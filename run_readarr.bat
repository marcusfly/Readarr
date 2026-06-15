@echo off
setlocal

set "ROOT=%~dp0"
set "SCRIPT=%ROOT%scripts\RunReadarrLatest.ps1"

powershell -NoProfile -ExecutionPolicy Bypass -NoLogo -File "%SCRIPT%" %*

exit /b %ERRORLEVEL%

