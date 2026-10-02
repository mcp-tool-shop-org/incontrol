<#
.SYNOPSIS
    Phase 2 audit harness for InControl.
.DESCRIPTION
    Verifies all Phase 2 acceptance gates are satisfied.
    Runs build, tests, coverage, and validates architecture.
.EXAMPLE
    .\scripts\audit.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$script:exitCode = 0
$script:gatesPassed = 0
$script:gatesFailed = 0

function Write-Gate {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Message
    )

    if ($Passed) {
        Write-Host "  [PASS] Gate: $Name" -ForegroundColor Green
        Write-Host "         $Message" -ForegroundColor Gray
        $script:gatesPassed++
    } else {
        Write-Host "  [FAIL] Gate: $Name" -ForegroundColor Red
        Write-Host "         $Message" -ForegroundColor Yellow
        $script:gatesFailed++
        $script:exitCode = 1
    }
}

function Test-Gate1-BuildIntegrity {
    Write-Host "`n=== Gate 1: Build & Test Integrity ===" -ForegroundColor Cyan

    # Check verify scripts exist
    $verifyPs1 = Test-Path "scripts/verify.ps1"
    $verifySh = Test-Path "scripts/verify.sh"
    Write-Gate "Verify scripts exist" ($verifyPs1 -and $verifySh) "verify.ps1=$verifyPs1, verify.sh=$verifySh"

    # Check .gitignore
    $gitignore = Test-Path ".gitignore"
    Write-Gate ".gitignore exists" $gitignore "Path: .gitignore"

    # Check no bin/obj in git (suppress expected error output)
    $binFiles = git ls-files "*/bin/*" 2>$null
    $objFiles = git ls-files "*/obj/*" 2>$null
    $noBinObj = [string]::IsNullOrEmpty($binFiles) -and [string]::IsNullOrEmpty($objFiles)
    Write-Gate "No bin/obj committed" $noBinObj "Build artifacts excluded from git"

    # Run build
    Write-Host "  Running build..." -ForegroundColor Gray
    $buildResult = dotnet build --configuration Release --verbosity quiet 2>&1
    $buildSuccess = ($LASTEXITCODE -eq 0)
    Write-Gate "Build succeeds" $buildSuccess "dotnet build --configuration Release"

    # Run tests
    Write-Host "  Running tests..." -ForegroundColor Gray
    $testResult = dotnet test --configuration Release --no-build --verbosity quiet 2>&1
    $testSuccess = ($LASTEXITCODE -eq 0)
    Write-Gate "Tests pass" $testSuccess "dotnet test --configuration Release"
}

function Test-Gate2-TestCoverage {
    Write-Host "`n=== Gate 2: Test Coverage Floor ===" -ForegroundColor Cyan

    # Check coverage scripts exist
    $coveragePs1 = Test-Path "scripts/coverage.ps1"
    $coverageSh = Test-Path "scripts/coverage.sh"
    Write-Gate "Coverage scripts exist" ($coveragePs1 -and $coverageSh) "coverage.ps1=$coveragePs1, coverage.sh=$coverageSh"

    # Check tests/Directory.Build.props exists
    $testsBuildProps = Test-Path "tests/Directory.Build.props"
    Write-Gate "tests/Directory.Build.props exists" $testsBuildProps "Coverlet configuration"

    # Count tests
    $coreTests = (Get-ChildItem -Path "tests/InControl.Core.Tests" -Filter "*.cs" -Recurse | Select-String -Pattern "\[Fact\]" | Measure-Object).Count
    $servicesTests = (Get-ChildItem -Path "tests/InControl.Services.Tests" -Filter "*.cs" -Recurse | Select-String -Pattern "\[Fact\]" | Measure-Object).Count
    $inferenceTests = (Get-ChildItem -Path "tests/InControl.Inference.Tests" -Filter "*.cs" -Recurse | Select-String -Pattern "\[Fact\]" | Measure-Object).Count
    $totalTests = $coreTests + $servicesTests + $inferenceTests

    Write-Gate "Substantial test coverage" ($totalTests -ge 100) "Found $totalTests tests (Core=$coreTests, Services=$servicesTests, Inference=$inferenceTests)"
}

function Test-Gate3-ExecutionBoundary {
    Write-Host "`n=== Gate 3: Execution Boundary Enforcement ===" -ForegroundColor Cyan

    # Check IFileStore exists
    $fileStoreInterface = Test-Path "src/InControl.Services/Storage/IFileStore.cs"
    Write-Gate "IFileStore interface exists" $fileStoreInterface "Path boundary abstraction"

    # Check FileStore implementation
    $fileStoreImpl = Test-Path "src/InControl.Services/Storage/FileStore.cs"
    Write-Gate "FileStore implementation exists" $fileStoreImpl "Path boundary enforcement"

    # Check path validation in FileStore
    if ($fileStoreImpl) {
        $fileStoreContent = Get-Content "src/InControl.Services/Storage/FileStore.cs" -Raw
        $hasPathValidation = $fileStoreContent -match "\.\..*PathNotAllowed|IsPathAllowed"
        Write-Gate "Path traversal blocked" $hasPathValidation "Validates paths contain no .."
    }

    # Check FakeInferenceClient exists
    $fakeClient = Test-Path "src/InControl.Inference/Fakes/FakeInferenceClient.cs"
    Write-Gate "FakeInferenceClient exists" $fakeClient "Testing without network"
}

function Test-Gate4-DeterministicState {
    Write-Host "`n=== Gate 4: Deterministic State & Persistence ===" -ForegroundColor Cyan

    # Check AppState exists
    $appState = Test-Path "src/InControl.Core/State/AppState.cs"
    Write-Gate "AppState exists" $appState "Root state container"

    # Check StateSerializer exists
    $serializer = Test-Path "src/InControl.Core/State/StateSerializer.cs"
    Write-Gate "StateSerializer exists" $serializer "Deterministic JSON"

    # Check serialization tests
    $serializationTests = Test-Path "tests/InControl.Core.Tests/State/SerializationTests.cs"
    Write-Gate "Serialization tests exist" $serializationTests "Round-trip verification"

    # Check immutability (sealed record pattern)
    if ($appState) {
        $appStateContent = Get-Content "src/InControl.Core/State/AppState.cs" -Raw
        $isImmutable = $appStateContent -match "sealed record.*AppState"
        Write-Gate "AppState is immutable" $isImmutable "sealed record pattern"
    }
}

function Test-Gate5-HealthAndErrors {
    Write-Host "`n=== Gate 5: Health, Errors, and Failure Clarity ===" -ForegroundColor Cyan

    # Check InControlError exists
    $voltError = Test-Path "src/InControl.Core/Errors/InControlError.cs"
    Write-Gate "InControlError exists" $voltError "Structured error type"

    # Check Result<T> exists
    $result = Test-Path "src/InControl.Core/Errors/Result.cs"
    Write-Gate "Result<T> exists" $result "Monadic error handling"

    # Check ErrorCode enum
    $errorCode = Test-Path "src/InControl.Core/Errors/ErrorCode.cs"
    Write-Gate "ErrorCode enum exists" $errorCode "Error taxonomy"

    # Check IHealthCheck exists
    $healthCheck = Test-Path "src/InControl.Services/Health/IHealthCheck.cs"
    Write-Gate "IHealthCheck exists" $healthCheck "Health probe interface"

    # Check HealthService exists
    $healthService = Test-Path "src/InControl.Services/Health/HealthService.cs"
    Write-Gate "HealthService exists" $healthService "Probe aggregation"

    # Check health checks
    $inferenceHealth = Test-Path "src/InControl.Services/Health/InferenceHealthCheck.cs"
    $storageHealth = Test-Path "src/InControl.Services/Health/StorageHealthCheck.cs"
    $appHealth = Test-Path "src/InControl.Services/Health/AppHealthCheck.cs"
    Write-Gate "Concrete health checks exist" ($inferenceHealth -and $storageHealth -and $appHealth) "Inference, Storage, App"
}

function Test-Gate6-ArchitectureLock {
    Write-Host "`n=== Gate 6: Architecture Lock ===" -ForegroundColor Cyan

    # Check ARCHITECTURE.md exists
    $archDoc = Test-Path "docs/ARCHITECTURE.md"
    Write-Gate "ARCHITECTURE.md exists" $archDoc "Architecture documentation"

    # Check PHASE2_ACCEPTANCE.md exists
    $phase2Doc = Test-Path "docs/PHASE2_ACCEPTANCE.md"
    Write-Gate "PHASE2_ACCEPTANCE.md exists" $phase2Doc "Phase 2 gate status"

    # Check out-of-scope section
    if ($archDoc) {
        $archContent = Get-Content "docs/ARCHITECTURE.md" -Raw
        $hasOutOfScope = $archContent -match "Out-of-Scope"
        Write-Gate "Out-of-scope documented" $hasOutOfScope "Explicit boundaries"

        $hasExtensionPoints = $archContent -match "Extension Points"
        Write-Gate "Extension points documented" $hasExtensionPoints "Future extensibility"

        $hasNonGoals = $archContent -match "Non-Goals"
        Write-Gate "Non-goals documented" $hasNonGoals "Explicit non-goals"
    }
}

function Test-Gate7-TrustSignals {
    Write-Host "`n=== Gate 7: User-Visible Trust Signals ===" -ForegroundColor Cyan

    # Check BuildInfo exists
    $buildInfo = Test-Path "src/InControl.Core/Trust/BuildInfo.cs"
    Write-Gate "BuildInfo exists" $buildInfo "Version and commit info"

    # Check TrustReport exists
    $trustReport = Test-Path "src/InControl.Core/Trust/TrustReport.cs"
    Write-Gate "TrustReport exists" $trustReport "Self-audit capability"

    # Check trust tests
    $buildInfoTests = Test-Path "tests/InControl.Core.Tests/Trust/BuildInfoTests.cs"
    $trustReportTests = Test-Path "tests/InControl.Core.Tests/Trust/TrustReportTests.cs"
    Write-Gate "Trust tests exist" ($buildInfoTests -and $trustReportTests) "Verification tests"
}

# Main execution
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "       InControl Phase 2 Audit Harness          " -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Running from: $(Get-Location)"
Write-Host "Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

# Run all gate checks
Test-Gate1-BuildIntegrity
Test-Gate2-TestCoverage
Test-Gate3-ExecutionBoundary
Test-Gate4-DeterministicState
Test-Gate5-HealthAndErrors
Test-Gate6-ArchitectureLock
Test-Gate7-TrustSignals

# Summary
Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "                 SUMMARY                    " -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Gates Passed: $script:gatesPassed" -ForegroundColor Green
Write-Host "Gates Failed: $script:gatesFailed" -ForegroundColor $(if ($script:gatesFailed -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($script:exitCode -eq 0) {
    Write-Host "Phase 2 Audit: PASSED" -ForegroundColor Green
    Write-Host ""
    Write-Host "All enforced gates satisfied." -ForegroundColor Gray
    Write-Host "Gate 6 (Architecture Lock) requires human sign-off." -ForegroundColor Yellow
} else {
    Write-Host "Phase 2 Audit: FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "Review failed gates above." -ForegroundColor Yellow
}

exit $script:exitCode
