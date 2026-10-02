<#
.SYNOPSIS
    Runs tests with coverage collection and generates reports.

.DESCRIPTION
    Executes all test projects with Coverlet code coverage,
    generates HTML reports via ReportGenerator, and optionally
    enforces coverage thresholds.

.PARAMETER Threshold
    Enforce minimum coverage percentage. Build fails if not met.

.PARAMETER OpenReport
    Opens the HTML coverage report after generation.

.EXAMPLE
    .\scripts\coverage.ps1
    .\scripts\coverage.ps1 -Threshold 70
    .\scripts\coverage.ps1 -OpenReport
#>

[CmdletBinding()]
param(
    [int]$Threshold = 0,
    [switch]$OpenReport
)

$ErrorActionPreference = "Stop"
$script:exitCode = 0

$repoRoot = Split-Path $PSScriptRoot -Parent
$coverageDir = Join-Path $repoRoot "coverage"
$reportDir = Join-Path $coverageDir "report"

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
Write-Host "`nInControl Coverage Script" -ForegroundColor Yellow
Write-Host "====================" -ForegroundColor Yellow

# Clean previous coverage
Write-Step "Cleaning previous coverage data"
if (Test-Path $coverageDir) {
    Remove-Item -Recurse -Force $coverageDir
}
New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null
Write-Success "Coverage directory cleaned"

# Build first
Write-Step "Building solution"
Push-Location $repoRoot
dotnet build -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Failure "Build failed"
    Pop-Location
    exit 1
}
Write-Success "Build complete"

# Run tests with coverage using collector
Write-Step "Running tests with coverage"

dotnet test -c Release --no-build `
    --collect:"XPlat Code Coverage" `
    --results-directory "$coverageDir" `
    --verbosity quiet

if ($LASTEXITCODE -eq 0) {
    Write-Success "All tests passed"
} else {
    Write-Failure "Some tests failed"
}

Pop-Location

# Find coverage files
$coverageFiles = Get-ChildItem -Path $coverageDir -Filter "coverage.cobertura.xml" -Recurse

if (-not $coverageFiles) {
    Write-Failure "No coverage files generated"
    exit 1
}

Write-Host "Found $($coverageFiles.Count) coverage file(s)" -ForegroundColor Gray

# Generate HTML report
Write-Step "Generating HTML report"

# Check if ReportGenerator is available as a tool
$reportGenTool = Get-Command reportgenerator -ErrorAction SilentlyContinue

if (-not $reportGenTool) {
    Write-Host "Installing ReportGenerator as global tool..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-reportgenerator-globaltool 2>$null
}

$coveragePattern = ($coverageFiles | ForEach-Object { $_.FullName }) -join ";"

reportgenerator `
    "-reports:$coveragePattern" `
    "-targetdir:$reportDir" `
    "-reporttypes:Html;TextSummary;Cobertura" `
    "-title:InControl Coverage Report"

if ($LASTEXITCODE -eq 0) {
    Write-Success "Report generated at: $reportDir\index.html"
} else {
    Write-Failure "Report generation failed"
}

# Display summary
Write-Step "Coverage Summary"
$summaryFile = Join-Path $reportDir "Summary.txt"
if (Test-Path $summaryFile) {
    Get-Content $summaryFile | ForEach-Object {
        if ($_ -match "Line coverage|InControl\.") {
            Write-Host $_ -ForegroundColor White
        } else {
            Write-Host $_
        }
    }
}

# Threshold enforcement
if ($Threshold -gt 0) {
    Write-Step "Threshold Enforcement"

    # Parse merged coverage from ReportGenerator output
    $mergedCoverage = Join-Path $reportDir "Cobertura.xml"
    if (Test-Path $mergedCoverage) {
        [xml]$coverageXml = Get-Content $mergedCoverage
        $lineRate = [math]::Round([double]$coverageXml.coverage.'line-rate' * 100, 2)

        Write-Host "Current line coverage: $lineRate%"
        Write-Host "Required threshold:    $Threshold%"

        if ($lineRate -lt $Threshold) {
            Write-Failure "Coverage $lineRate% is below threshold $Threshold%"
        } else {
            Write-Success "Coverage meets threshold"
        }
    }
}

# Open report
if ($OpenReport -and (Test-Path "$reportDir\index.html")) {
    Start-Process "$reportDir\index.html"
}

# Summary
Write-Host "`n"
if ($script:exitCode -eq 0) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  COVERAGE COMPLETE" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  COVERAGE FAILED" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
}

exit $script:exitCode
