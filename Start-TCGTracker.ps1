<#
.SYNOPSIS
    Launches Trading Card Game Tracker in single-server mode on Windows.

.DESCRIPTION
    Supports both running from a published self-contained release folder (api.exe)
    and running from the source repository root (builds client-vite into api/wwwroot
    when missing or when -Rebuild is passed, applies EF migrations, starts the API,
    waits for /api/health, and opens the default browser).

.PARAMETER Rebuild
    Forces a clean client-vite build before starting the server (repository mode only).

.PARAMETER Url
    Optional base URL to probe and open in the browser. Defaults to http://localhost:5229
    in both standalone release and source repository modes.

.PARAMETER NoBrowser
    Starts the server and waits for /api/health without opening the browser.
#>
[CmdletBinding()]
param(
    [switch]$Rebuild,
    [string]$Url,
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot
Set-Location $RepoRoot

function Stop-ProcessTree {
    param([int]$ProcessId)
    try {
        $children = Get-CimInstance Win32_Process -Filter "ParentProcessId = $ProcessId" -ErrorAction SilentlyContinue
        foreach ($child in $children) {
            Stop-ProcessTree -ProcessId $child.ProcessId
        }
        Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
    }
    catch {
        Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Format-ProcessArgument {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value)) {
        return '""'
    }
    if ($Value -notmatch '[\s"]') {
        return $Value
    }
    $escaped = $Value -replace '(\\*)"', '$1$1\"'
    $escaped = $escaped -replace '(\\+)$', '$1$1'
    return '"' + $escaped + '"'
}

function Test-HealthEndpoint {
    param([string]$HealthUrl)
    try {
        if ($PSVersionTable.PSVersion.Major -ge 7) {
            $resp = Invoke-WebRequest -Uri $HealthUrl -SkipCertificateCheck -TimeoutSec 2 -UseBasicParsing
            return $resp.StatusCode -eq 200
        }

        $previousCallback = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
        try {
            if ($HealthUrl.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase)) {
                [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
            }
            $resp = Invoke-WebRequest -Uri $HealthUrl -TimeoutSec 2 -UseBasicParsing
            return $resp.StatusCode -eq 200
        }
        finally {
            [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $previousCallback
        }
    }
    catch {
        return $false
    }
}

$standaloneExe = Join-Path $RepoRoot "api.exe"
$isStandalone = Test-Path $standaloneExe

if ([string]::IsNullOrWhiteSpace($Url)) {
    $Url = "http://localhost:5229"
}
$Url = $Url.TrimEnd("/")

$serverProcess = $null

try {
    if ($isStandalone) {
        Write-Host "Starting Trading Card Game Tracker (standalone) at $Url ..." -ForegroundColor Cyan
        $standaloneArgs = (@("--urls", $Url, "--contentRoot", $RepoRoot) | ForEach-Object { Format-ProcessArgument $_ }) -join " "
        $serverProcess = Start-Process -FilePath $standaloneExe `
            -ArgumentList $standaloneArgs `
            -WorkingDirectory $RepoRoot `
            -PassThru `
            -NoNewWindow
    }
    else {
        if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
            Write-Error ".NET SDK ('dotnet') was not found on PATH. Install .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0"
        }

        $apiDir = Join-Path $RepoRoot "api"
        $apiProject = Join-Path $apiDir "api.csproj"
        $wwwrootIndex = Join-Path $apiDir "wwwroot\index.html"
        $clientDir = Join-Path $RepoRoot "client-vite"
        $needsFrontendBuild = $Rebuild -or (-not (Test-Path $wwwrootIndex))

        if ($needsFrontendBuild) {
            if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
                Write-Error "Node.js / npm was not found on PATH, and api/wwwroot/index.html needs to be built. Install Node.js 20+ from https://nodejs.org/"
            }

            Push-Location $clientDir
            try {
                if ($Rebuild -or (-not (Test-Path (Join-Path $clientDir "node_modules")))) {
                    Write-Host "Installing frontend dependencies (npm ci)..." -ForegroundColor Cyan
                    & npm ci
                    if ($LASTEXITCODE -ne 0) {
                        throw "npm ci failed with exit code $LASTEXITCODE"
                    }
                }

                Write-Host "Building frontend into api/wwwroot (npm run build)..." -ForegroundColor Cyan
                & npm run build
                if ($LASTEXITCODE -ne 0) {
                    throw "npm run build failed with exit code $LASTEXITCODE"
                }
            }
            finally {
                Pop-Location
            }
        }
        else {
            Write-Host "Using existing api/wwwroot/index.html (pass -Rebuild to force a frontend rebuild)." -ForegroundColor DarkGray
        }

        Write-Host "Applying database migrations..." -ForegroundColor Cyan
        Push-Location $apiDir
        try {
            & dotnet run --project $apiProject --no-launch-profile -- seed
            if ($LASTEXITCODE -ne 0) {
                throw "Database migration failed with exit code $LASTEXITCODE"
            }
        }
        finally {
            Pop-Location
        }

        Write-Host "Starting Trading Card Game Tracker server at $Url ..." -ForegroundColor Cyan
        $serverArgs = (@(
            "run",
            "--project", $apiProject,
            "--no-launch-profile",
            "--no-build",
            "--",
            "--urls", $Url,
            "--contentRoot", $apiDir
        ) | ForEach-Object { Format-ProcessArgument $_ }) -join " "

        $serverProcess = Start-Process -FilePath "dotnet" `
            -ArgumentList $serverArgs `
            -WorkingDirectory $apiDir `
            -PassThru `
            -NoNewWindow
    }

    $healthUrl = "$Url/api/health"
    $deadline = (Get-Date).AddSeconds(60)
    $ready = $false

    Write-Host "Waiting for server readiness at $healthUrl ..." -ForegroundColor Cyan
    while ((Get-Date) -lt $deadline) {
        if ($serverProcess.HasExited) {
            throw "Server process exited unexpectedly with code $($serverProcess.ExitCode)."
        }
        if (Test-HealthEndpoint -HealthUrl $healthUrl) {
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 500
    }

    if (-not $ready) {
        throw "Timed out waiting for $healthUrl to respond."
    }

    Write-Host "Trading Card Game Tracker is running at $Url" -ForegroundColor Green
    if (-not $NoBrowser) {
        Start-Process $Url | Out-Null
    }

    Write-Host "Press Ctrl+C to stop the server." -ForegroundColor Yellow
    Wait-Process -Id $serverProcess.Id
}
finally {
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Write-Host "`nStopping Trading Card Game Tracker server..." -ForegroundColor Cyan
        Stop-ProcessTree -ProcessId $serverProcess.Id
    }
}
