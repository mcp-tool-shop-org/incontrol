<#
.SYNOPSIS
    Verifies InControl builds and tests cleanly from a fresh clone.

.DESCRIPTION
    This script runs the complete build and test pipeline to ensure
    the repository is in a reproducible state. Use this before commits
    or to verify a fresh clone works correctly.

.EXAMPLE
    .\scripts\verify.ps1
#>

[CmdletBinding()]
param(
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
    Write-Host "[PASS] $Message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Message)
    Write-Host "[FAIL] $Message" -ForegroundColor Red
    $script:exitCode = 1
}

# Header
Write-Host "`nInControl Verification Script" -ForegroundColor Yellow
Write-Host "========================" -ForegroundColor Yellow

# Step 1: Environment info
Write-Step "Environment"
dotnet --info | Select-Object -First 10
Write-Success "SDK detected"

# Step 2: Clean (optional, for true fresh verification)
Write-Step "Cleaning previous build artifacts"
if (Test-Path "src/*/bin") { Remove-Item -Recurse -Force "src/*/bin" }
if (Test-Path "src/*/obj") { Remove-Item -Recurse -Force "src/*/obj" }
if (Test-Path "tests/*/bin") { Remove-Item -Recurse -Force "tests/*/bin" }
if (Test-Path "tests/*/obj") { Remove-Item -Recurse -Force "tests/*/obj" }
Write-Success "Clean complete"

# Step 3: Restore
Write-Step "Restoring packages"
dotnet restore
if ($LASTEXITCODE -eq 0) {
    Write-Success "Restore complete"
} else {
    Write-Failure "Restore failed"
}

# Step 4: Build
Write-Step "Building solution (Release)"
dotnet build -c Release --no-restore
if ($LASTEXITCODE -eq 0) {
    Write-Success "Build complete"
} else {
    Write-Failure "Build failed"
}

# Step 5: Tests
if (-not $SkipTests) {
    Write-Step "Running tests"
    dotnet test -c Release --no-build --verbosity normal
    if ($LASTEXITCODE -eq 0) {
        Write-Success "All tests passed"
    } else {
        Write-Failure "Tests failed"
    }
} else {
    Write-Host "`nSkipping tests (-SkipTests specified)" -ForegroundColor Yellow
}

# Step 6: Verify git status is clean (no generated files tracked)
Write-Step "Checking git status"
$gitStatus = git status --porcelain
if ([string]::IsNullOrWhiteSpace($gitStatus)) {
    Write-Success "Working tree clean"
} else {
    Write-Host "Untracked or modified files detected:" -ForegroundColor Yellow
    Write-Host $gitStatus
    # Not a failure - just informational
}

# Summary
Write-Host "`n" -NoNewline
if ($script:exitCode -eq 0) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  VERIFICATION PASSED" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  VERIFICATION FAILED" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
}

exit $script:exitCode
