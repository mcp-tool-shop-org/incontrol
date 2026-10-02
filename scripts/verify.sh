#!/bin/bash
#
# Verifies InControl builds and tests cleanly from a fresh clone.
#
# Usage: ./scripts/verify.sh [--skip-tests]
#

set -e

SKIP_TESTS=false
EXIT_CODE=0

# Parse arguments
for arg in "$@"; do
    case $arg in
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
    esac
done

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

step() {
    echo -e "\n${CYAN}=== $1 ===${NC}"
}

pass() {
    echo -e "${GREEN}[PASS]${NC} $1"
}

fail() {
    echo -e "${RED}[FAIL]${NC} $1"
    EXIT_CODE=1
}

# Header
echo -e "\n${YELLOW}InControl Verification Script${NC}"
echo -e "${YELLOW}========================${NC}"

# Step 1: Environment
step "Environment"
dotnet --info | head -10
pass "SDK detected"

# Step 2: Clean
step "Cleaning previous build artifacts"
rm -rf src/*/bin src/*/obj tests/*/bin tests/*/obj 2>/dev/null || true
pass "Clean complete"

# Step 3: Restore
step "Restoring packages"
if dotnet restore; then
    pass "Restore complete"
else
    fail "Restore failed"
fi

# Step 4: Build
step "Building solution (Release)"
if dotnet build -c Release --no-restore; then
    pass "Build complete"
else
    fail "Build failed"
fi

# Step 5: Tests
if [ "$SKIP_TESTS" = false ]; then
    step "Running tests"
    if dotnet test -c Release --no-build --verbosity normal; then
        pass "All tests passed"
    else
        fail "Tests failed"
    fi
else
    echo -e "\n${YELLOW}Skipping tests (--skip-tests specified)${NC}"
fi

# Step 6: Git status
step "Checking git status"
GIT_STATUS=$(git status --porcelain)
if [ -z "$GIT_STATUS" ]; then
    pass "Working tree clean"
else
    echo -e "${YELLOW}Untracked or modified files detected:${NC}"
    echo "$GIT_STATUS"
fi

# Summary
echo ""
if [ $EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}  VERIFICATION PASSED${NC}"
    echo -e "${GREEN}========================================${NC}"
else
    echo -e "${RED}========================================${NC}"
    echo -e "${RED}  VERIFICATION FAILED${NC}"
    echo -e "${RED}========================================${NC}"
fi

exit $EXIT_CODE
