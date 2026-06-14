$OutputCsv  = "P:\Git\readarr\magazines\magazines_com_catalog.csv"
$BaseUrl    = "https://www.magazines.com"
$CatalogUrl = "https://www.magazines.com/searchdata/products.json"
$DelayMs    = 750
$TimeoutSec = 30
$MaxItems   = 0   # Set to 0 for full scrape

Set-StrictMode -Off
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Get-Prop {
    param(
        [AllowNull()] $Object,
        [Parameter(Mandatory)] [string] $Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) {
        return $null
    }

    return $prop.Value
}

function Get-FirstNonEmpty {
    param([object[]] $Values)

    foreach ($value in $Values) {
        if ($null -ne $value -and -not [string]::IsNullOrWhiteSpace([string]$value)) {
            return $value
        }
    }

    return $null
}

function New-MagazineWebSession {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $session.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36"
    return $session
}

function Invoke-GetText {
    param(
        [Parameter(Mandatory)] [string] $Uri,
        [Parameter(Mandatory)] [Microsoft.PowerShell.Commands.WebRequestSession] $Session,
        [int] $TimeoutSec = 30
    )

    $headers = @{
        "Accept"          = "text/html,application/xhtml+xml,application/xml;q=0.9,application/json;q=0.8,*/*;q=0.7"
        "Accept-Language" = "en-US,en;q=0.9"
        "Cache-Control"   = "no-cache"
    }

    $response = Invoke-WebRequest `
        -Uri $Uri `
        -WebSession $Session `
        -Headers $headers `
        -TimeoutSec $TimeoutSec `
        -MaximumRedirection 5 `
        -UseBasicParsing

    return $response.Content
}

function Normalize-ProductUrl {
    param(
        [Parameter(Mandatory)] [string] $Url,
        [Parameter(Mandatory)] [string] $BaseUrl
    )

    if ($Url.StartsWith("http", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $Url
    }

    if (-not $Url.StartsWith("/")) {
        $Url = "/" + $Url
    }

    return $BaseUrl.TrimEnd("/") + $Url
}

function Get-JsonLdObjects {
    param([Parameter(Mandatory)] [string] $Html)

    $objects = New-Object System.Collections.Generic.List[object]
    $pattern = '(?is)<script[^>]+type=["'']application/ld\+json["''][^>]*>(.*?)</script>'
    $matches = [regex]::Matches($Html, $pattern)

    foreach ($match in $matches) {
        $jsonText = [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value).Trim()

        if ([string]::IsNullOrWhiteSpace($jsonText)) {
            continue
        }

        try {
            $parsed = $jsonText | ConvertFrom-Json

            if ($parsed -is [System.Array]) {
                foreach ($item in $parsed) {
                    $objects.Add($item) | Out-Null
                }
            }
            else {
                $objects.Add($parsed) | Out-Null
            }
        }
        catch {
            Write-Warning "JSON-LD parse failed."
        }
    }

    return $objects
}

function Get-FirstJsonLdProduct {
    param([object[]] $JsonLdObjects)

    foreach ($obj in $JsonLdObjects) {
        $type = Get-Prop $obj "@type"
        if ([string]$type -eq "Product") {
            return $obj
        }
    }

    return $null
}

function Get-CategoryBreadcrumb {
    param([object[]] $JsonLdObjects)

    foreach ($obj in $JsonLdObjects) {
        $type = Get-Prop $obj "@type"
        if ([string]$type -ne "BreadcrumbList") {
            continue
        }

        $itemList = Get-Prop $obj "itemListElement"
        if ($null -eq $itemList) {
            continue
        }

        $names = @()

        foreach ($item in $itemList) {
            $name = Get-Prop $item "name"
            if ($null -ne $name) {
                $names += [string]$name
            }
        }

        if ($names.Count -gt 0) {
            return ($names -join " > ")
        }
    }

    return $null
}

function Get-ProductIdFromHtml {
    param([Parameter(Mandatory)] [string] $Html)

    $match = [regex]::Match($Html, 'data-products=["''](?<id>prod\d+)["'']')
    if ($match.Success) {
        return $match.Groups["id"].Value
    }

    $match = [regex]::Match($Html, '"sku"\s*:\s*"(?<id>prod\d+)"')
    if ($match.Success) {
        return $match.Groups["id"].Value
    }

    return $null
}

function Get-SelectionDataObjects {
    param([Parameter(Mandatory)] [string] $Html)

    $results = New-Object System.Collections.Generic.List[object]

    $pattern = '(?is)<input[^>]+name=["'']selectionData["''][^>]+value=(["''])(.*?)\1'
    $matches = [regex]::Matches($Html, $pattern)

    foreach ($match in $matches) {
        $rawValue = [System.Net.WebUtility]::HtmlDecode($match.Groups[2].Value).Trim()

        if ([string]::IsNullOrWhiteSpace($rawValue)) {
            continue
        }

        try {
            $outer = $rawValue | ConvertFrom-Json

            foreach ($prop in $outer.PSObject.Properties) {
                try {
                    $inner = $prop.Name | ConvertFrom-Json
                    $results.Add($inner) | Out-Null
                }
                catch {
                    Write-Warning "Inner selectionData parse failed."
                }
            }
        }
        catch {
            Write-Warning "Outer selectionData parse failed."
        }
    }

    return $results
}

function New-Row {
    param(
        [Parameter(Mandatory)] $CatalogItem,
        [Parameter(Mandatory)] [string] $ProductUrl,
        [AllowNull()] $JsonLdProduct,
        [AllowNull()] $SelectionData,
        [AllowNull()] $MagazineItem,
        [AllowNull()] [string] $ProductId,
        [AllowNull()] [string] $Breadcrumb,
        [AllowNull()] [string] $ErrorMessage
    )

    $brand = Get-Prop $JsonLdProduct "brand"
    $brandName = $null

    if ($brand -is [string]) {
        $brandName = $brand
    }
    elseif ($null -ne $brand) {
        $brandName = Get-Prop $brand "name"
    }

    $offers = Get-Prop $JsonLdProduct "offers"
    $aggregateRating = Get-Prop $JsonLdProduct "aggregateRating"

    $displayName = Get-Prop $MagazineItem "displayName"
    if ($null -ne $displayName) {
        $displayName = [System.Net.WebUtility]::UrlDecode([string]$displayName)
    }

    return [pscustomobject]@{
        source                = "magazines.com"
        scraped_at_utc        = (Get-Date).ToUniversalTime().ToString("o")

        catalog_title         = Get-Prop $CatalogItem "title"
        catalog_url           = Get-Prop $CatalogItem "url"
        product_url           = $ProductUrl
        catalog_image         = Get-Prop $CatalogItem "image"
        catalog_popularity    = Get-Prop $CatalogItem "popularity"

        product_name          = Get-Prop $JsonLdProduct "name"
        display_name          = $displayName
        brand                 = $brandName
        description           = Get-Prop $JsonLdProduct "description"
        breadcrumb            = $Breadcrumb

        product_id            = Get-FirstNonEmpty @(
                                    $ProductId,
                                    (Get-Prop $JsonLdProduct "sku")
                                )

        sku_id                = Get-Prop $MagazineItem "skuId"
        selection_id          = Get-Prop $SelectionData "selectionId"
        mag_code              = Get-Prop $MagazineItem "magCode"
        key                   = Get-Prop $MagazineItem "key"

        gtin14                = Get-Prop $JsonLdProduct "gtin14"

        image_large           = Get-Prop $JsonLdProduct "image"
        image_small           = Get-Prop $CatalogItem "image"

        price                 = Get-FirstNonEmpty @(
                                    (Get-Prop $MagazineItem "price"),
                                    (Get-Prop $offers "price"),
                                    (Get-Prop $SelectionData "totalRenewalPrice")
                                )

        currency              = Get-Prop $offers "priceCurrency"
        availability          = Get-Prop $offers "availability"

        num_issues            = Get-Prop $MagazineItem "numIssues"
        magazine_sku_type     = Get-Prop $MagazineItem "magazineSkuType"

        recurring             = Get-Prop $SelectionData "recurring"
        auto_renew            = Get-Prop $SelectionData "autoRenew"
        ship_to_po_box        = Get-Prop $SelectionData "shipToPOBox"

        cds_magazine          = Get-Prop $MagazineItem "cdsMagazine"
        subco_magazine        = Get-Prop $MagazineItem "subcoMagazine"

        taxable_sku           = Get-Prop $MagazineItem "taxableSku"
        tax_code              = Get-Prop $MagazineItem "taxCode"
        tax_status            = Get-Prop $MagazineItem "taxStatus"
        shipping_and_handling = Get-Prop $MagazineItem "shippingAndHandling"

        rating_value          = Get-Prop $aggregateRating "ratingValue"
        review_count          = Get-Prop $aggregateRating "reviewCount"

        scrape_error          = $ErrorMessage
    }
}

$workDir = Split-Path $OutputCsv -Parent
if (-not (Test-Path $workDir)) {
    New-Item -ItemType Directory -Path $workDir -Force | Out-Null
}

$session = New-MagazineWebSession

Write-Host "Downloading catalog index: $CatalogUrl"
$catalogText = Invoke-GetText -Uri $CatalogUrl -Session $session -TimeoutSec $TimeoutSec
$catalog = $catalogText | ConvertFrom-Json

if ($null -eq $catalog) {
    throw "Catalog returned no data."
}

$items = @($catalog)

if ($MaxItems -gt 0) {
    $items = @($items | Select-Object -First $MaxItems)
}

Write-Host "Catalog items: $($items.Count)"

$rows = New-Object System.Collections.Generic.List[object]
$index = 0

foreach ($item in $items) {
    $index++

    $relativeUrl = Get-Prop $item "url"
    $title = Get-Prop $item "title"
    $productUrl = Normalize-ProductUrl -Url ([string]$relativeUrl) -BaseUrl $BaseUrl

    Write-Progress `
        -Activity "Scraping Magazines.com catalog" `
        -Status "$index / $($items.Count): $title" `
        -PercentComplete (($index / [double]$items.Count) * 100)

    try {
        Start-Sleep -Milliseconds $DelayMs

        $html = Invoke-GetText -Uri $productUrl -Session $session -TimeoutSec $TimeoutSec

        $jsonLdObjects = @(Get-JsonLdObjects -Html $html)
        $product = Get-FirstJsonLdProduct -JsonLdObjects $jsonLdObjects
        $breadcrumb = Get-CategoryBreadcrumb -JsonLdObjects $jsonLdObjects
        $productId = Get-ProductIdFromHtml -Html $html
        $selectionDataObjects = @(Get-SelectionDataObjects -Html $html)

        if ($selectionDataObjects.Count -eq 0) {
            $rows.Add((New-Row `
                -CatalogItem $item `
                -ProductUrl $productUrl `
                -JsonLdProduct $product `
                -SelectionData $null `
                -MagazineItem $null `
                -ProductId $productId `
                -Breadcrumb $breadcrumb `
                -ErrorMessage $null)) | Out-Null

            continue
        }

        foreach ($selection in $selectionDataObjects) {
            $magazineItems = Get-Prop $selection "magazineItems"

            if ($null -eq $magazineItems -or @($magazineItems).Count -eq 0) {
                $rows.Add((New-Row `
                    -CatalogItem $item `
                    -ProductUrl $productUrl `
                    -JsonLdProduct $product `
                    -SelectionData $selection `
                    -MagazineItem $null `
                    -ProductId $productId `
                    -Breadcrumb $breadcrumb `
                    -ErrorMessage $null)) | Out-Null

                continue
            }

            foreach ($magItem in @($magazineItems)) {
                $rows.Add((New-Row `
                    -CatalogItem $item `
                    -ProductUrl $productUrl `
                    -JsonLdProduct $product `
                    -SelectionData $selection `
                    -MagazineItem $magItem `
                    -ProductId $productId `
                    -Breadcrumb $breadcrumb `
                    -ErrorMessage $null)) | Out-Null
            }
        }
    }
    catch {
        Write-Warning "Failed: $productUrl :: $($_.Exception.Message)"

        $rows.Add((New-Row `
            -CatalogItem $item `
            -ProductUrl $productUrl `
            -JsonLdProduct $null `
            -SelectionData $null `
            -MagazineItem $null `
            -ProductId $null `
            -Breadcrumb $null `
            -ErrorMessage $_.Exception.Message)) | Out-Null
    }
}

Write-Progress -Activity "Scraping Magazines.com catalog" -Completed

$rows |
    Sort-Object catalog_title, magazine_sku_type, mag_code |
    Export-Csv -Path $OutputCsv -NoTypeInformation -Encoding UTF8

Write-Host "Done."
Write-Host "Rows written: $($rows.Count)"
Write-Host "CSV: $OutputCsv"

