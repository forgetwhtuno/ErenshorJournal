param(
    [string]$GameDir = "",
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"
if (-not $GameDir) {
    throw "Pass -GameDir 'C:\path\to\Erenshor'. Journal data is preserved unless -RemoveData is also supplied."
}

$dll = Join-Path $GameDir "plugins\ErenshorJournal.dll"
if (Test-Path $dll) {
    Remove-Item $dll -Force
    Write-Host "Removed Erenshor Journal plugin file." -ForegroundColor Green
}

if ($RemoveData) {
    $dataDir = Join-Path $GameDir "plugins\config\ErenshorJournal"
    if (Test-Path $dataDir) {
        Remove-Item $dataDir -Recurse -Force
        Write-Host "Removed local journal data." -ForegroundColor Yellow
    }
}
else {
    Write-Host "Local journal data was preserved. Use -RemoveData only if you intentionally want to delete your notes." -ForegroundColor Cyan
}
