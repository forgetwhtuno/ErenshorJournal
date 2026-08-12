param(
    [string]$BepInExRoot = "",
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"
if (-not $BepInExRoot) {
    throw "Pass -BepInExRoot 'C:\path\to\your\Erenshor profile'. Journal data is preserved unless -RemoveData is also supplied."
}

$pluginDir = Join-Path $BepInExRoot "BepInEx\plugins\ErenshorJournal"
if (Test-Path $pluginDir) {
    Remove-Item $pluginDir -Recurse -Force
    Write-Host "Removed Erenshor Journal plugin files." -ForegroundColor Green
}

if ($RemoveData) {
    $dataDir = Join-Path $BepInExRoot "BepInEx\config\ErenshorJournal"
    if (Test-Path $dataDir) {
        Remove-Item $dataDir -Recurse -Force
        Write-Host "Removed local journal data." -ForegroundColor Yellow
    }
}
else {
    Write-Host "Local journal data was preserved. Use -RemoveData only if you intentionally want to delete your notes." -ForegroundColor Cyan
}
