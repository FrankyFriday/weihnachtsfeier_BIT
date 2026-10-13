$ErrorActionPreference = "Continue"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "WeihnachtsfeierBIT/WeihnachtsfeierBit.slnx"
$composePath = Join-Path $repoRoot "WeihnachtsfeierBIT/docker-compose.yml"
$script:failed = $false

function Invoke-Check {
    param(
        [string]$Name,
        [string]$Executable,
        [string[]]$Arguments
    )

    Write-Host "`n==> $Name"
    if (-not (Get-Command -Name $Executable -ErrorAction SilentlyContinue)) {
        Write-Host "FAIL: '$Executable' was not found."
        $script:failed = $true
        return
    }

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: $Name (exit code $LASTEXITCODE)"
        $script:failed = $true
    }
    else {
        Write-Host "PASS: $Name"
    }
}

Invoke-Check -Name "Solution build" -Executable "dotnet" -Arguments @("build", $solutionPath)
Invoke-Check -Name "Compose configuration" -Executable "docker" -Arguments @("compose", "-f", $composePath, "config", "--quiet")

if ($script:failed) {
    exit 1
}

Write-Host "`nAll lightweight validation checks passed."
exit 0