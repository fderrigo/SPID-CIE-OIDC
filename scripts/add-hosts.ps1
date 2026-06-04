# Adds the federation hostnames to the Windows hosts file. Run as Administrator.
#   PowerShell (admin):  ./scripts/add-hosts.ps1
$ErrorActionPreference = "Stop"
$hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
$line = "127.0.0.1`ttrust-anchor.org relying-party.org cie-provider.org"

$current = Get-Content $hostsPath -Raw
if ($current -match "trust-anchor\.org") {
    Write-Host "Hosts entries already present. Nothing to do." -ForegroundColor Yellow
} else {
    Add-Content -Path $hostsPath -Value "`r`n# SPID/CIE OIDC local test`r`n$line"
    Write-Host "Added federation hostnames to $hostsPath" -ForegroundColor Green
}
Write-Host "Verify:" -ForegroundColor Cyan
Select-String -Path $hostsPath -Pattern "trust-anchor.org"
