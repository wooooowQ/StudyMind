param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Resolve-Path -LiteralPath (Join-Path $scriptDir "..")
$backendDir = Join-Path $root "backend"
$frontendProject = Join-Path $root "frontend\StudyMind.App\StudyMind.App.csproj"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root "dist\StudyMind"
}

$outputFullPath = [System.IO.Path]::GetFullPath($OutputDir)
$rootFullPath = [System.IO.Path]::GetFullPath($root)

if (-not $outputFullPath.StartsWith($rootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDir must stay inside the workspace: $outputFullPath"
}

if ($outputFullPath -eq $rootFullPath) {
    throw "OutputDir cannot be the workspace root."
}

$backendOut = Join-Path $outputFullPath "backend"
$frontendOut = Join-Path $outputFullPath "frontend"
$dataOut = Join-Path $outputFullPath "data"

if (Test-Path -LiteralPath $outputFullPath) {
    Get-ChildItem -LiteralPath $outputFullPath -Force |
        Where-Object { $_.Name -ne "data" } |
        Remove-Item -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $backendOut, $frontendOut, $dataOut | Out-Null

Push-Location $backendDir
try {
    cargo build --release
}
finally {
    Pop-Location
}

$backendExe = Join-Path $backendDir "target\release\studymind-backend.exe"
if (-not (Test-Path -LiteralPath $backendExe)) {
    throw "Backend executable was not found: $backendExe"
}
Copy-Item -LiteralPath $backendExe -Destination (Join-Path $backendOut "studymind-backend.exe") -Force

dotnet publish $frontendProject `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained false `
    -o $frontendOut

$launcherPath = Join-Path $outputFullPath "start-studymind.ps1"
@'
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$backendExe = Join-Path $root "backend\studymind-backend.exe"
$frontendExe = Join-Path $root "frontend\StudyMind.App.exe"
$stateRoot = if ($env:LOCALAPPDATA) { $env:LOCALAPPDATA } else { Join-Path $root "data" }
$dataDir = Join-Path $stateRoot "StudyMind"
$databasePath = Join-Path $dataDir "studymind.db"
$legacyDataDir = Join-Path $root "data"
$legacyDatabasePath = Join-Path $root "data\studymind.db"

New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
if ((-not (Test-Path -LiteralPath $databasePath)) -and (Test-Path -LiteralPath $legacyDatabasePath)) {
    Get-ChildItem -LiteralPath $legacyDataDir -Filter "studymind.db*" -Force |
        ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $dataDir $_.Name) -Force
        }
}

$env:STUDYMIND_BIND_ADDR = "127.0.0.1:7878"
$env:STUDYMIND_DATABASE_PATH = $databasePath

function Test-StudyMindBackend {
    try {
        return Invoke-RestMethod -Uri "http://127.0.0.1:7878/health" -TimeoutSec 2
    }
    catch {
        return $null
    }
}

function Assert-ExpectedDatabase($Health) {
    if ($null -eq $Health) {
        return
    }

    $actual = [System.IO.Path]::GetFullPath([string]$Health.database)
    $expected = [System.IO.Path]::GetFullPath($databasePath)
    if (-not [string]::Equals($actual, $expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Port 7878 is already used by another StudyMind backend: $actual"
    }
}

$backend = $null
$startedBackend = $false

try {
    $existing = Test-StudyMindBackend
    if ($existing) {
        Assert-ExpectedDatabase $existing
    }
    else {
        $backend = Start-Process `
            -FilePath $backendExe `
            -WorkingDirectory (Split-Path -Parent $backendExe) `
            -WindowStyle Hidden `
            -PassThru
        $startedBackend = $true
    }

    $healthy = $false
    for ($i = 0; $i -lt 40; $i++) {
        if ($backend -and $backend.HasExited) {
            throw "StudyMind backend exited during startup with code $($backend.ExitCode)."
        }

        $health = Test-StudyMindBackend
        if ($health) {
            Assert-ExpectedDatabase $health
            $healthy = $true
            break
        }

        Start-Sleep -Milliseconds 250
    }

    if (-not $healthy) {
        throw "StudyMind backend did not become healthy."
    }

    $frontend = Start-Process -FilePath $frontendExe -WorkingDirectory (Split-Path -Parent $frontendExe) -PassThru
    $backendPid = if ($backend) { $backend.Id } else { "existing" }
    Write-Host "StudyMind started. Backend PID: $backendPid"
    $frontend.WaitForExit()
}
finally {
    if ($startedBackend -and $backend -and -not $backend.HasExited) {
        Stop-Process -Id $backend.Id -Force
    }
}
'@ | Set-Content -LiteralPath $launcherPath -Encoding UTF8

$releaseNotesPath = Join-Path $outputFullPath "README-RELEASE.txt"
@"
StudyMind local release

How to start:
1. Run frontend\StudyMind.App.exe. The app starts the local backend automatically.
2. You can still run start-studymind.ps1 with PowerShell if you prefer the old one-click launcher.
3. The backend listens on http://127.0.0.1:7878.
4. Local data is stored in %LOCALAPPDATA%\StudyMind\studymind.db.
5. When the app starts the backend itself, it stops that backend after the WinUI app exits.

Included files:
- backend\studymind-backend.exe
- frontend\StudyMind.App.exe and WinUI runtime output
- data\ reserved for migrating older release-folder databases
- start-studymind.ps1
"@ | Set-Content -LiteralPath $releaseNotesPath -Encoding UTF8

Write-Host "Release package created: $outputFullPath"
