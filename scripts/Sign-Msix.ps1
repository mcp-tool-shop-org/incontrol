# Sign-Msix.ps1 - Sign MSIX with self-signed certificate

$ErrorActionPreference = "Stop"

$msixPath = "artifacts\msix\InControl-Desktop-0.9.0-rc.2.msix"
$thumbprint = "FBF2F3F607DD39655B55D0E6B3DC74FEC6F4F0D6"

# Find signtool
$signtool = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" |
    Sort-Object { [version]($_.Directory.Parent.Name) } -Descending |
    Select-Object -First 1

Write-Host "Using signtool: $($signtool.FullName)"
Write-Host "Signing: $msixPath"
Write-Host "Certificate: $thumbprint"

# Sign the MSIX
& $signtool.FullName sign /fd SHA256 /sha1 $thumbprint /tr http://timestamp.digicert.com /td SHA256 $msixPath

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Package signed successfully!" -ForegroundColor Green

    # Verify
    Write-Host ""
    Write-Host "Verifying signature..."
    & $signtool.FullName verify /pa $msixPath
} else {
    Write-Error "Signing failed"
}
