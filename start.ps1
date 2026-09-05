# Starts the Cheater Watcher stack. On first run it creates .env from .env.example
# (with a random PostgreSQL password) so you don't have to configure anything by hand.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$envPath = Join-Path $root ".env"

if (Test-Path $envPath) {
    Write-Host "Using existing .env"
} else {
    Copy-Item (Join-Path $root ".env.example") $envPath
    $password = -join ((1..16) | ForEach-Object { "{0:x2}" -f (Get-Random -Maximum 256) })
    $content = Get-Content $envPath | ForEach-Object {
        if ($_ -match "^POSTGRES_PASSWORD=.*") { "POSTGRES_PASSWORD=$password" } else { $_ }
    }
    Set-Content -Path $envPath -Value $content
    Write-Host "Created .env from .env.example with a random POSTGRES_PASSWORD"
}

docker compose -f (Join-Path $root "docker-compose.yml") up -d --build