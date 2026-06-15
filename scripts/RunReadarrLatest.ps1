param(
    [string]$DataPath = "",
    [string]$Branch = "develop-mfly",
    [string]$BindHost = "127.0.0.1",
    [int]$Port = 8787,
    [int]$TimeoutSeconds = 120,
    [bool]$UseDocker = $true,
    [string]$ComposeFile = "docker-compose.yml"
)

$ErrorActionPreference = "Stop"

$rootPath = Split-Path -Parent $PSScriptRoot
Set-Location -Path $rootPath

$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne $Branch) {
    Write-Host "Current branch '$currentBranch' is not '$Branch'. You are not running this machine's requested branch."
}

if ([string]::IsNullOrWhiteSpace($DataPath)) {
    $DataPath = Join-Path $rootPath ".readarr-data"
}

if (-not (Test-Path $DataPath)) {
    New-Item -ItemType Directory -Path $DataPath | Out-Null
}

$pidFile = Join-Path $DataPath "readarr-host.pid"
$listenPort = [Math]::Min(65535, [Math]::Max(1, $Port))
$url = "http://$BindHost`:$listenPort/ping"

function Invoke-ReadarrLocally {
    param(
        [int]$listenPort,
        [string]$ReadyUrl,
        [string]$DataDirectory,
        [int]$TimeoutSeconds
    )

    Write-Host "Stopping tracked Readarr process."
    if (Test-Path $pidFile) {
        try {
            $trackedPid = [int](Get-Content $pidFile -ErrorAction Stop)
            if (Get-Process -Id $trackedPid -ErrorAction SilentlyContinue) {
                Stop-Process -Id $trackedPid -Force
                Start-Sleep -Milliseconds 250
            }
        } catch {
            # ignore stale pid files or races
        }
        Remove-Item $pidFile -ErrorAction SilentlyContinue
    }

    try {
        $boundPid = Get-NetTCPConnection -State Listen -LocalPort $listenPort -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($id in $boundPid) {
            $proc = Get-Process -Id $id -ErrorAction SilentlyContinue
            if ($proc -and $proc.ProcessName -eq "dotnet") {
                Stop-Process -Id $id -Force
                Start-Sleep -Milliseconds 250
            }
        }
    } catch {
        # ignore environments where network inspection is restricted
    }

    Start-Sleep -Milliseconds 250

    Write-Host "Building Readarr Console (Debug, no restore)."
    dotnet build src\NzbDrone.Console\Readarr.Console.csproj -c Debug --nologo --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Readarr build failed with exit code $LASTEXITCODE."
    }

    $assembly = Join-Path $rootPath "_output\net10.0\Readarr.Console.dll"
    if (-not (Test-Path $assembly)) {
        throw "Build artifact not found: $assembly"
    }

    Write-Host "Launching Readarr from: $assembly"
    $proc = Start-Process -FilePath "dotnet" -ArgumentList @(
        "`"$assembly`"",
        "/data=`"$DataDirectory`"",
        "/nobrowser"
    ) -PassThru

    $ready = $false
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $ReadyUrl -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }

    if (-not $ready) {
        throw "Readarr did not become available at $ReadyUrl within $TimeoutSeconds seconds."
    }

    Write-Host "Readarr is running and responding."
    Write-Host "Open: $($ReadyUrl -replace '/ping$', '')"
    Write-Host "PID: $($proc.Id)"
    Set-Content -Path $pidFile -Value $proc.Id -Encoding ASCII
}

function Invoke-ReadarrWithDocker {
    param(
        [string]$ComposeFilePath,
        [string]$ReadyUrl,
        [int]$TimeoutSeconds
    )

    if (-not (Test-Path $ComposeFilePath)) {
        throw "Compose file not found: $ComposeFilePath"
    }

    $composeFile = Resolve-Path $ComposeFilePath | Select-Object -ExpandProperty Path
    Write-Host "Using Docker compose for startup: $composeFile"

    Write-Host "Stopping existing readarr container."
    $downResult = Invoke-DockerCommand -Arguments @("compose", "-f", $composeFile, "down", "--remove-orphans")
    if ($downResult.Code -ne 0) {
        throw "docker compose down failed with exit code $($downResult.Code)."
    }

    Write-Host "Starting Readarr container."
    $buildResult = Invoke-DockerCommand -Arguments @("compose", "-f", $composeFile, "build")
    if ($buildResult.Code -ne 0) {
        throw "docker compose build failed with exit code $($buildResult.Code)."
    }

    Write-Host "Launching Readarr container."
    $upResult = Invoke-DockerCommand -Arguments @("compose", "-f", $composeFile, "up", "-d", "--force-recreate")
    if ($upResult.Code -ne 0) {
        throw "docker compose up failed with exit code $($upResult.Code)."
    }

    $ready = $false
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $ReadyUrl -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }

    if (-not $ready) {
        throw "Readarr did not become available at $ReadyUrl within $TimeoutSeconds seconds."
    }

    Write-Host "Readarr is running in Docker and responding."
    Write-Host "Open: $($ReadyUrl -replace '/ping$', '')"
}

if ($UseDocker) {
    $dockerConfigDir = Join-Path $rootPath ".docker"
    if (-not (Test-Path $dockerConfigDir)) {
        New-Item -ItemType Directory -Path $dockerConfigDir | Out-Null
    }

    $originalDockerConfig = $env:DOCKER_CONFIG
    $env:DOCKER_CONFIG = $dockerConfigDir

    try {
        function Invoke-DockerCommand {
            param(
                [Parameter(Mandatory)]
                [string[]]$Arguments
            )

            $previousAction = $ErrorActionPreference
            $ErrorActionPreference = "Continue"

            $output = & docker @Arguments 2>&1 | Out-String

            $ErrorActionPreference = $previousAction
            return @{
                Code = $LASTEXITCODE
                Output = $output
            }
        }

        $versionResult = Invoke-DockerCommand -Arguments @("version")
        if ($versionResult.Code -ne 0) {
            throw "docker command failed with exit code $($versionResult.Code)."
        }
        Invoke-ReadarrWithDocker -ComposeFilePath $ComposeFile -ReadyUrl $url -TimeoutSeconds $TimeoutSeconds
    } catch {
        Write-Host "[readarr-latest] Docker unavailable, falling back to local dotnet launch. $($_.Exception.Message)"
        Invoke-ReadarrLocally -listenPort $listenPort -ReadyUrl $url -DataDirectory $DataPath -TimeoutSeconds $TimeoutSeconds
    } finally {
        if ($null -ne $originalDockerConfig) {
            $env:DOCKER_CONFIG = $originalDockerConfig
        } else {
            Remove-Item Env:DOCKER_CONFIG -ErrorAction SilentlyContinue
        }
    }
} else {
    Invoke-ReadarrLocally -listenPort $listenPort -ReadyUrl $url -DataDirectory $DataPath -TimeoutSeconds $TimeoutSeconds
}
