$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $root "backend"

Push-Location $backend
try {
    if (-not $env:STUDYMIND_DATABASE_PATH) {
        $env:STUDYMIND_DATABASE_PATH = "data\studymind.db"
    }

    if (-not $env:STUDYMIND_BIND_ADDR) {
        $env:STUDYMIND_BIND_ADDR = "127.0.0.1:7878"
    }

    cargo run
}
finally {
    Pop-Location
}
