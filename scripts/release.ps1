<#
.SYNOPSIS
    Creates a release build of InControl-Desktop.

.DESCRIPTION
    This script produces release artifacts in a deterministic folder layout.
    It builds the solution in Release configuration, creates MSIX package,
    and organizes outputs for distribution.

.PARAMETER Version
    The version string to use (e.g., "0.4.0"). If not specified, uses
    the version from Directory.Build.props.

.PARAMETER OutputPath
    The directory where release artifacts will be placed.
    Default: ./artifacts

.PARAMETER SkipTests
    Skip running tests before release build.

.EXAMPLE
    .\scripts\release.ps1
    .\scripts\release.ps1 -Version "1.0.0" -OutputPath "./release"
#>

[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputPath = "./artifacts",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$script:exitCode = 0

function Write-Step {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Message)
    Write-Host "[FAIL] $Message" -ForegroundColor Red
    $script:exitCode = 1
}

function Get-VersionFromProps {
    $propsPath = Join-Path $PSScriptRoot ".." "Directory.Build.props"
    [xml]$props = Get-Content $propsPath
    $prefix = $props.Project.PropertyGroup.VersionPrefix | Where-Object { $_ }
    $suffix = $props.Project.PropertyGroup.VersionSuffix | Where-Object { $_ }

    if ($suffix -and $suffix -ne '') {
        return "$prefix-$suffix"
    }
    return $prefix
}

# Header
Write-Host "`n======================================" -ForegroundColor Yellow
Write-Host "  InControl-Desktop Release Build" -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Yellow

# Determine version
if (-not $Version) {
    $Version = Get-VersionFromProps
}
Write-Host "Version: $Version" -ForegroundColor White

# Timestamp for reproducibility tracking
$buildTime = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
Write-Host "Build time: $buildTime" -ForegroundColor Gray

# Step 1: Clean previous artifacts
Write-Step "Cleaning previous artifacts"
if (Test-Path $OutputPath) {
    Remove-Item -Recurse -Force $OutputPath
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Create subfolder structure
$binPath = Join-Path $OutputPath "bin"
$msixPath = Join-Path $OutputPath "msix"
$logsPath = Join-Path $OutputPath "logs"
New-Item -ItemType Directory -Path $binPath -Force | Out-Null
New-Item -ItemType Directory -Path $msixPath -Force | Out-Null
New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
Write-Success "Artifact directories created"

# Step 2: Restore
Write-Step "Restoring packages"
$restoreLog = Join-Path $logsPath "restore.log"
dotnet restore 2>&1 | Tee-Object -FilePath $restoreLog
if ($LASTEXITCODE -eq 0) {
    Write-Success "Restore complete"
} else {
    Write-Failure "Restore failed"
    exit 1
}

# Step 3: Build Release
Write-Step "Building Release configuration"
$buildLog = Join-Path $logsPath "build.log"
dotnet build -c Release --no-restore /p:Version=$Version 2>&1 | Tee-Object -FilePath $buildLog
if ($LASTEXITCODE -eq 0) {
    Write-Success "Build complete"
} else {
    Write-Failure "Build failed"
    exit 1
}

# Step 4: Run tests (unless skipped)
if (-not $SkipTests) {
    Write-Step "Running tests"
    $testLog = Join-Path $logsPath "test.log"
    dotnet test -c Release --no-build --verbosity normal 2>&1 | Tee-Object -FilePath $testLog
    if ($LASTEXITCODE -eq 0) {
        Write-Success "All tests passed"
    } else {
        Write-Failure "Tests failed"
        exit 1
    }
} else {
    Write-Host "`nSkipping tests (-SkipTests specified)" -ForegroundColor Yellow
}

# Step 5: Publish application
Write-Step "Publishing application"
$publishPath = Join-Path $binPath "InControl"
$publishLog = Join-Path $logsPath "publish.log"
dotnet publish src/InControl.App/InControl.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishPath `
    /p:Version=$Version `
    /p:PublishSingleFile=false `
    2>&1 | Tee-Object -FilePath $publishLog

if ($LASTEXITCODE -eq 0) {
    Write-Success "Publish complete"
} else {
    Write-Failure "Publish failed"
    exit 1
}

# Step 6: Create version manifest
Write-Step "Creating version manifest"
$manifest = @{
    product = "InControl-Desktop"
    version = $Version
    buildTime = $buildTime
    configuration = "Release"
    runtime = "win-x64"
    dotnetVersion = (dotnet --version)
    gitCommit = (git rev-parse HEAD 2>$null)
    gitBranch = (git rev-parse --abbrev-ref HEAD 2>$null)
}
$manifestPath = Join-Path $OutputPath "version.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding UTF8
Write-Success "Version manifest created: $manifestPath"

# Step 7: Calculate checksums
Write-Step "Calculating checksums"
$checksumPath = Join-Path $OutputPath "checksums.sha256"
$files = Get-ChildItem -Path $publishPath -File -Recurse
$checksums = @()
foreach ($file in $files) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash
    $relativePath = $file.FullName.Substring($publishPath.Length + 1)
    $checksums += "$hash  $relativePath"
}
$checksums | Set-Content $checksumPath -Encoding UTF8
Write-Success "Checksums written: $checksumPath"

# Summary
Write-Host "`n======================================" -ForegroundColor Green
Write-Host "  RELEASE BUILD COMPLETE" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Version:    $Version"
Write-Host "Output:     $OutputPath"
Write-Host "Binaries:   $publishPath"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Review artifacts in $OutputPath"
Write-Host "  2. Create MSIX package (if needed)"
Write-Host "  3. Test on clean machine"
Write-Host ""

exit $script:exitCode
