$ErrorActionPreference = "Continue"
$repoRoot = Split-Path -Parent $PSScriptRoot
$validationScript = Join-Path $PSScriptRoot "validate.ps1"
$composePath = Join-Path $repoRoot "WeihnachtsfeierBIT/docker-compose.yml"
$currentPowerShell = (Get-Process -Id $PID).Path

Write-Host "==> Lightweight validation"
& $currentPowerShell -NoProfile -File $validationScript
if ($LASTEXITCODE -ne 0) {
    Write-Host "Validation failed; Compose services were not updated."
    exit $LASTEXITCODE
}

if (-not (Get-Command -Name "docker" -ErrorAction SilentlyContinue)) {
    Write-Host "FAIL: 'docker' was not found."
    exit 1
}

$expectedServices = @(& docker compose -f $composePath config --services)
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL: Could not read Compose services."
    exit $LASTEXITCODE
}

Write-Host "`n==> Build and start all Compose services"
& docker compose -f $composePath up --build --detach
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL: Compose could not build or start the services."
    exit $LASTEXITCODE
}

$runningServices = @(& docker compose -f $composePath ps --services --status running)
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL: Could not read running Compose services."
    exit $LASTEXITCODE
}

$missingServices = @($expectedServices | Where-Object { $_ -notin $runningServices })
if ($missingServices.Count -gt 0) {
    Write-Host "FAIL: Services are not running: $($missingServices -join ', ')"
    exit 1
}

Write-Host "`nAll Compose services are running. The developer can now perform final functional tests."
exit 0