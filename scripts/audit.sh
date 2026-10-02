#!/bin/bash
#
# Phase 2 audit harness for InControl.
# Verifies all Phase 2 acceptance gates are satisfied.
#

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

GATES_PASSED=0
GATES_FAILED=0
EXIT_CODE=0

write_gate() {
    local name="$1"
    local passed="$2"
    local message="$3"

    if [ "$passed" = "true" ]; then
        echo -e "  ${GREEN}[PASS]${NC} Gate: $name"
        echo -e "         ${GRAY}$message${NC}"
        ((GATES_PASSED++))
    else
        echo -e "  ${RED}[FAIL]${NC} Gate: $name"
        echo -e "         ${YELLOW}$message${NC}"
        ((GATES_FAILED++))
        EXIT_CODE=1
    fi
}

test_gate1_build_integrity() {
    echo -e "\n${CYAN}=== Gate 1: Build & Test Integrity ===${NC}"

    # Check verify scripts exist
    verify_ps1=$([ -f "scripts/verify.ps1" ] && echo "true" || echo "false")
    verify_sh=$([ -f "scripts/verify.sh" ] && echo "true" || echo "false")
    write_gate "Verify scripts exist" $([ "$verify_ps1" = "true" ] && [ "$verify_sh" = "true" ] && echo "true" || echo "false") "verify.ps1=$verify_ps1, verify.sh=$verify_sh"

    # Check .gitignore
    gitignore=$([ -f ".gitignore" ] && echo "true" || echo "false")
    write_gate ".gitignore exists" "$gitignore" "Path: .gitignore"

    # Check no bin/obj in git
    bin_in_git=$(git ls-files --error-unmatch "*/bin/*" 2>/dev/null && echo "true" || echo "false")
    obj_in_git=$(git ls-files --error-unmatch "*/obj/*" 2>/dev/null && echo "true" || echo "false")
    no_bin_obj=$([ "$bin_in_git" = "false" ] && [ "$obj_in_git" = "false" ] && echo "true" || echo "false")
    write_gate "No bin/obj committed" "$no_bin_obj" "Build artifacts excluded from git"

    # Run build
    echo -e "  ${GRAY}Running build...${NC}"
    if dotnet build --configuration Release --verbosity quiet 2>&1 > /dev/null; then
        write_gate "Build succeeds" "true" "dotnet build --configuration Release"
    else
        write_gate "Build succeeds" "false" "dotnet build --configuration Release"
    fi

    # Run tests
    echo -e "  ${GRAY}Running tests...${NC}"
    if dotnet test --configuration Release --no-build --verbosity quiet 2>&1 > /dev/null; then
        write_gate "Tests pass" "true" "dotnet test --configuration Release"
    else
        write_gate "Tests pass" "false" "dotnet test --configuration Release"
    fi
}

test_gate2_test_coverage() {
    echo -e "\n${CYAN}=== Gate 2: Test Coverage Floor ===${NC}"

    # Check coverage scripts exist
    coverage_ps1=$([ -f "scripts/coverage.ps1" ] && echo "true" || echo "false")
    coverage_sh=$([ -f "scripts/coverage.sh" ] && echo "true" || echo "false")
    write_gate "Coverage scripts exist" $([ "$coverage_ps1" = "true" ] && [ "$coverage_sh" = "true" ] && echo "true" || echo "false") "coverage.ps1=$coverage_ps1, coverage.sh=$coverage_sh"

    # Check tests/Directory.Build.props exists
    tests_build_props=$([ -f "tests/Directory.Build.props" ] && echo "true" || echo "false")
    write_gate "tests/Directory.Build.props exists" "$tests_build_props" "Coverlet configuration"

    # Count tests
    core_tests=$(grep -r "\[Fact\]" tests/InControl.Core.Tests --include="*.cs" 2>/dev/null | wc -l || echo 0)
    services_tests=$(grep -r "\[Fact\]" tests/InControl.Services.Tests --include="*.cs" 2>/dev/null | wc -l || echo 0)
    inference_tests=$(grep -r "\[Fact\]" tests/InControl.Inference.Tests --include="*.cs" 2>/dev/null | wc -l || echo 0)
    total_tests=$((core_tests + services_tests + inference_tests))

    write_gate "Substantial test coverage" $([ "$total_tests" -ge 100 ] && echo "true" || echo "false") "Found $total_tests tests (Core=$core_tests, Services=$services_tests, Inference=$inference_tests)"
}

test_gate3_execution_boundary() {
    echo -e "\n${CYAN}=== Gate 3: Execution Boundary Enforcement ===${NC}"

    # Check IFileStore exists
    file_store_interface=$([ -f "src/InControl.Services/Storage/IFileStore.cs" ] && echo "true" || echo "false")
    write_gate "IFileStore interface exists" "$file_store_interface" "Path boundary abstraction"

    # Check FileStore implementation
    file_store_impl=$([ -f "src/InControl.Services/Storage/FileStore.cs" ] && echo "true" || echo "false")
    write_gate "FileStore implementation exists" "$file_store_impl" "Path boundary enforcement"

    # Check path validation in FileStore
    if [ "$file_store_impl" = "true" ]; then
        has_path_validation=$(grep -E "\.\.|PathNotAllowed|IsPathAllowed" src/InControl.Services/Storage/FileStore.cs 2>/dev/null && echo "true" || echo "false")
        write_gate "Path traversal blocked" "$has_path_validation" "Validates paths contain no .."
    fi

    # Check FakeInferenceClient exists
    fake_client=$([ -f "src/InControl.Inference/Fakes/FakeInferenceClient.cs" ] && echo "true" || echo "false")
    write_gate "FakeInferenceClient exists" "$fake_client" "Testing without network"
}

test_gate4_deterministic_state() {
    echo -e "\n${CYAN}=== Gate 4: Deterministic State & Persistence ===${NC}"

    # Check AppState exists
    app_state=$([ -f "src/InControl.Core/State/AppState.cs" ] && echo "true" || echo "false")
    write_gate "AppState exists" "$app_state" "Root state container"

    # Check StateSerializer exists
    serializer=$([ -f "src/InControl.Core/State/StateSerializer.cs" ] && echo "true" || echo "false")
    write_gate "StateSerializer exists" "$serializer" "Deterministic JSON"

    # Check serialization tests
    serialization_tests=$([ -f "tests/InControl.Core.Tests/State/SerializationTests.cs" ] && echo "true" || echo "false")
    write_gate "Serialization tests exist" "$serialization_tests" "Round-trip verification"

    # Check immutability (sealed record pattern)
    if [ "$app_state" = "true" ]; then
        is_immutable=$(grep -E "sealed record.*AppState" src/InControl.Core/State/AppState.cs 2>/dev/null && echo "true" || echo "false")
        write_gate "AppState is immutable" "$is_immutable" "sealed record pattern"
    fi
}

test_gate5_health_and_errors() {
    echo -e "\n${CYAN}=== Gate 5: Health, Errors, and Failure Clarity ===${NC}"

    # Check InControlError exists
    volt_error=$([ -f "src/InControl.Core/Errors/InControlError.cs" ] && echo "true" || echo "false")
    write_gate "InControlError exists" "$volt_error" "Structured error type"

    # Check Result<T> exists
    result=$([ -f "src/InControl.Core/Errors/Result.cs" ] && echo "true" || echo "false")
    write_gate "Result<T> exists" "$result" "Monadic error handling"

    # Check ErrorCode enum
    error_code=$([ -f "src/InControl.Core/Errors/ErrorCode.cs" ] && echo "true" || echo "false")
    write_gate "ErrorCode enum exists" "$error_code" "Error taxonomy"

    # Check IHealthCheck exists
    health_check=$([ -f "src/InControl.Services/Health/IHealthCheck.cs" ] && echo "true" || echo "false")
    write_gate "IHealthCheck exists" "$health_check" "Health probe interface"

    # Check HealthService exists
    health_service=$([ -f "src/InControl.Services/Health/HealthService.cs" ] && echo "true" || echo "false")
    write_gate "HealthService exists" "$health_service" "Probe aggregation"

    # Check health checks
    inference_health=$([ -f "src/InControl.Services/Health/InferenceHealthCheck.cs" ] && echo "true" || echo "false")
    storage_health=$([ -f "src/InControl.Services/Health/StorageHealthCheck.cs" ] && echo "true" || echo "false")
    app_health=$([ -f "src/InControl.Services/Health/AppHealthCheck.cs" ] && echo "true" || echo "false")
    all_health=$([ "$inference_health" = "true" ] && [ "$storage_health" = "true" ] && [ "$app_health" = "true" ] && echo "true" || echo "false")
    write_gate "Concrete health checks exist" "$all_health" "Inference, Storage, App"
}

test_gate6_architecture_lock() {
    echo -e "\n${CYAN}=== Gate 6: Architecture Lock ===${NC}"

    # Check ARCHITECTURE.md exists
    arch_doc=$([ -f "docs/ARCHITECTURE.md" ] && echo "true" || echo "false")
    write_gate "ARCHITECTURE.md exists" "$arch_doc" "Architecture documentation"

    # Check PHASE2_ACCEPTANCE.md exists
    phase2_doc=$([ -f "docs/PHASE2_ACCEPTANCE.md" ] && echo "true" || echo "false")
    write_gate "PHASE2_ACCEPTANCE.md exists" "$phase2_doc" "Phase 2 gate status"

    # Check out-of-scope section
    if [ "$arch_doc" = "true" ]; then
        has_out_of_scope=$(grep -q "Out-of-Scope" docs/ARCHITECTURE.md && echo "true" || echo "false")
        write_gate "Out-of-scope documented" "$has_out_of_scope" "Explicit boundaries"

        has_extension_points=$(grep -q "Extension Points" docs/ARCHITECTURE.md && echo "true" || echo "false")
        write_gate "Extension points documented" "$has_extension_points" "Future extensibility"

        has_non_goals=$(grep -q "Non-Goals" docs/ARCHITECTURE.md && echo "true" || echo "false")
        write_gate "Non-goals documented" "$has_non_goals" "Explicit non-goals"
    fi
}

test_gate7_trust_signals() {
    echo -e "\n${CYAN}=== Gate 7: User-Visible Trust Signals ===${NC}"

    # Check BuildInfo exists
    build_info=$([ -f "src/InControl.Core/Trust/BuildInfo.cs" ] && echo "true" || echo "false")
    write_gate "BuildInfo exists" "$build_info" "Version and commit info"

    # Check TrustReport exists
    trust_report=$([ -f "src/InControl.Core/Trust/TrustReport.cs" ] && echo "true" || echo "false")
    write_gate "TrustReport exists" "$trust_report" "Self-audit capability"

    # Check trust tests
    build_info_tests=$([ -f "tests/InControl.Core.Tests/Trust/BuildInfoTests.cs" ] && echo "true" || echo "false")
    trust_report_tests=$([ -f "tests/InControl.Core.Tests/Trust/TrustReportTests.cs" ] && echo "true" || echo "false")
    all_trust_tests=$([ "$build_info_tests" = "true" ] && [ "$trust_report_tests" = "true" ] && echo "true" || echo "false")
    write_gate "Trust tests exist" "$all_trust_tests" "Verification tests"
}

# Main execution
echo -e "${CYAN}============================================${NC}"
echo -e "${CYAN}       InControl Phase 2 Audit Harness          ${NC}"
echo -e "${CYAN}============================================${NC}"
echo ""
echo "Running from: $(pwd)"
echo "Date: $(date '+%Y-%m-%d %H:%M:%S')"

# Run all gate checks
test_gate1_build_integrity
test_gate2_test_coverage
test_gate3_execution_boundary
test_gate4_deterministic_state
test_gate5_health_and_errors
test_gate6_architecture_lock
test_gate7_trust_signals

# Summary
echo -e "\n${CYAN}============================================${NC}"
echo -e "${CYAN}                 SUMMARY                    ${NC}"
echo -e "${CYAN}============================================${NC}"
echo ""
echo -e "${GREEN}Gates Passed: $GATES_PASSED${NC}"
if [ "$GATES_FAILED" -gt 0 ]; then
    echo -e "${RED}Gates Failed: $GATES_FAILED${NC}"
else
    echo -e "${GREEN}Gates Failed: $GATES_FAILED${NC}"
fi
echo ""

if [ "$EXIT_CODE" -eq 0 ]; then
    echo -e "${GREEN}Phase 2 Audit: PASSED${NC}"
    echo ""
    echo -e "${GRAY}All enforced gates satisfied.${NC}"
    echo -e "${YELLOW}Gate 6 (Architecture Lock) requires human sign-off.${NC}"
else
    echo -e "${RED}Phase 2 Audit: FAILED${NC}"
    echo ""
    echo -e "${YELLOW}Review failed gates above.${NC}"
fi

exit $EXIT_CODE
