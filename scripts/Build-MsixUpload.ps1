# Build-MsixUpload.ps1
#
# Builds an MSIXUPLOAD package for Microsoft Store submission.
# The .msixupload file contains the MSIX bundle and symbols.
#
# Usage: .\Build-MsixUpload.ps1 -Version 0.9.0-rc.1 -OutputPath ./artifacts
#
# Prerequisites:
# - Windows SDK installed (for makeappx.exe, makepri.exe)
# - .NET 9 SDK
# - Project builds successfully

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$OutputPath = "./artifacts",

    [string]$Configuration = "Release",

    [string]$Platform = "x64",

    [switch]$IncludeSymbols
)

$ErrorActionPreference = "Stop"
$ProjectPath = "src/InControl.App/InControl.App.csproj"

# Parse version for MSIX (must be x.y.z.w format)
$msixVersion = $Version -replace '-.*', ''
if ($msixVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    $msixVersion = "$msixVersion.0"
}

Write-Host "Building MSIXUPLOAD for InControl-Desktop" -ForegroundColor Cyan
Write-Host "  Version: $Version (MSIX: $msixVersion)" -ForegroundColor Gray
Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
Write-Host "  Platform: $Platform" -ForegroundColor Gray

# Create output directories
$publishPath = Join-Path $OutputPath "publish"
$msixPath = Join-Path $OutputPath "msix"
New-Item -ItemType Directory -Force -Path $publishPath | Out-Null
New-Item -ItemType Directory -Force -Path $msixPath | Out-Null

# Step 1: Build and publish
Write-Host "`n[1/5] Building and publishing..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    --configuration $Configuration `
    --runtime "win-$Platform" `
    --self-contained true `
    -p:Platform=$Platform `
    -p:Version=$Version `
    -p:AssemblyVersion=$msixVersion `
    -p:FileVersion=$msixVersion `
    --output $publishPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Step 2: Find Windows SDK tools
Write-Host "`n[2/5] Locating Windows SDK tools..." -ForegroundColor Yellow
$sdkPath = "C:\Program Files (x86)\Windows Kits\10\bin"
$makeappx = Get-ChildItem -Path "$sdkPath\*\$Platform\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object { [version]($_.Directory.Parent.Name) } -Descending |
    Select-Object -First 1

$makepri = Get-ChildItem -Path "$sdkPath\*\$Platform\makepri.exe" -ErrorAction SilentlyContinue |
    Sort-Object { [version]($_.Directory.Parent.Name) } -Descending |
    Select-Object -First 1

if (-not $makeappx) {
    Write-Error "makeappx.exe not found. Install Windows SDK."
    exit 1
}
Write-Host "  Using SDK: $($makeappx.Directory.Parent.Name)" -ForegroundColor Gray

# Step 3: Create AppxManifest.xml in publish folder
Write-Host "`n[3/5] Creating AppxManifest.xml..." -ForegroundColor Yellow

$manifestTemplate = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">

  <Identity
    Name="mcp-tool-shop.InControl-Desktop"
    Publisher="CN=5305D976-6952-4F00-9C21-3A5DB090359F"
    Version="$msixVersion"
    ProcessorArchitecture="$Platform" />

  <mp:PhoneIdentity PhoneProductId="a1b2c3d4-e5f6-7890-abcd-ef1234567890" PhonePublisherId="00000000-0000-0000-0000-000000000000"/>

  <Properties>
    <DisplayName>InControl-Desktop</DisplayName>
    <PublisherDisplayName>mcp-tool-shop</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22621.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>

  <Resources>
    <Resource Language="en-us"/>
  </Resources>

  <Applications>
    <Application Id="App"
      Executable="InControl.App.exe"
      EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="InControl"
        Description="Local AI Chat Assistant for Windows"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
        <uap:SplashScreen Image="Assets\SplashScreen.png"/>
      </uap:VisualElements>
    </Application>
  </Applications>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@

$manifestPath = Join-Path $publishPath "AppxManifest.xml"
$manifestTemplate | Out-File -FilePath $manifestPath -Encoding utf8
Write-Host "  Created: $manifestPath" -ForegroundColor Gray

# Step 4: Create placeholder assets if missing
Write-Host "`n[4/5] Checking assets..." -ForegroundColor Yellow
$assetsPath = Join-Path $publishPath "Assets"
if (-not (Test-Path $assetsPath)) {
    New-Item -ItemType Directory -Force -Path $assetsPath | Out-Null
    Write-Host "  Created Assets folder (add real assets before Store submission)" -ForegroundColor DarkYellow

    # Create placeholder PNG files (1x1 transparent)
    $placeholderAssets = @(
        "StoreLogo.png",
        "Square150x150Logo.png",
        "Square44x44Logo.png",
        "Wide310x150Logo.png",
        "SplashScreen.png"
    )

    # Minimal valid PNG (1x1 transparent)
    $pngBytes = [byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
                         0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                         0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
                         0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                         0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
                         0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82)

    foreach ($asset in $placeholderAssets) {
        $assetFile = Join-Path $assetsPath $asset
        [IO.File]::WriteAllBytes($assetFile, $pngBytes)
    }
    Write-Host "  Created placeholder assets" -ForegroundColor DarkYellow
}

# Step 5: Create MSIX package
Write-Host "`n[5/5] Creating MSIX package..." -ForegroundColor Yellow
$msixFile = Join-Path $msixPath "InControl-Desktop-$Version.msix"

& $makeappx.FullName pack /d $publishPath /p $msixFile /nv /o

if ($LASTEXITCODE -ne 0) {
    Write-Error "makeappx failed"
    exit 1
}

Write-Host "  Created: $msixFile" -ForegroundColor Green

# Create MSIXUPLOAD (just a renamed ZIP containing the MSIX)
Write-Host "`nCreating MSIXUPLOAD package..." -ForegroundColor Yellow
$uploadPath = Join-Path $OutputPath "InControl-Desktop-$Version.msixupload"

# MSIXUPLOAD is a ZIP containing the MSIX (and optionally symbols)
$tempDir = Join-Path $OutputPath "msixupload-temp"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
Copy-Item $msixFile -Destination $tempDir

if ($IncludeSymbols) {
    # Copy PDB files if requested
    Get-ChildItem -Path $publishPath -Filter "*.pdb" | ForEach-Object {
        Copy-Item $_.FullName -Destination $tempDir
    }
}

# Create as .zip first, then rename to .msixupload
$zipPath = Join-Path $OutputPath "InControl-Desktop-$Version.zip"
Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -Force
if (Test-Path $uploadPath) { Remove-Item $uploadPath -Force }
Rename-Item -Path $zipPath -NewName (Split-Path $uploadPath -Leaf)
Remove-Item -Path $tempDir -Recurse -Force

Write-Host "  Created: $uploadPath" -ForegroundColor Green

# Generate checksums
Write-Host "`nGenerating checksums..." -ForegroundColor Yellow
$checksumFile = Join-Path $OutputPath "checksums-$Version.txt"

@($msixFile, $uploadPath) | ForEach-Object {
    if (Test-Path $_) {
        $hash = (Get-FileHash -Path $_ -Algorithm SHA256).Hash.ToLower()
        $name = Split-Path $_ -Leaf
        "$hash  $name" | Out-File -FilePath $checksumFile -Append -Encoding utf8
    }
}

Write-Host "  Created: $checksumFile" -ForegroundColor Green

# Summary
Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "BUILD COMPLETE" -ForegroundColor Green
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host ""
Write-Host "Artifacts:" -ForegroundColor White
Get-ChildItem -Path $OutputPath -File | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  $($_.Name) ($sizeMB MB)" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Sign the MSIX with your code signing certificate" -ForegroundColor Gray
Write-Host "  2. Replace placeholder assets with real icons" -ForegroundColor Gray
Write-Host "  3. Upload .msixupload to Partner Center" -ForegroundColor Gray
