#!/bin/bash
#
# Runs tests with coverage collection and generates reports.
#
# Usage: ./scripts/coverage.sh [--threshold N] [--open]
#

set -e

THRESHOLD=0
OPEN_REPORT=false
EXIT_CODE=0

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --threshold)
            THRESHOLD="$2"
            shift 2
            ;;
        --open)
            OPEN_REPORT=true
            shift
            ;;
        *)
            shift
            ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."
COVERAGE_DIR="$REPO_ROOT/coverage"
REPORT_DIR="$COVERAGE_DIR/report"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

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
echo -e "\n${YELLOW}InControl Coverage Script${NC}"
echo -e "${YELLOW}====================${NC}"

# Clean previous coverage
step "Cleaning previous coverage data"
rm -rf "$COVERAGE_DIR"
mkdir -p "$COVERAGE_DIR"
pass "Coverage directory cleaned"

cd "$REPO_ROOT"

# Build first
step "Building solution"
if dotnet build -c Release --verbosity quiet; then
    pass "Build complete"
else
    fail "Build failed"
    exit 1
fi

# Run tests with coverage using collector
step "Running tests with coverage"

if dotnet test -c Release --no-build \
    --collect:"XPlat Code Coverage" \
    --results-directory "$COVERAGE_DIR" \
    --verbosity quiet; then
    pass "All tests passed"
else
    fail "Some tests failed"
fi

# Find coverage files
COVERAGE_FILES=$(find "$COVERAGE_DIR" -name "coverage.cobertura.xml" 2>/dev/null)

if [ -z "$COVERAGE_FILES" ]; then
    fail "No coverage files generated"
    exit 1
fi

FILE_COUNT=$(echo "$COVERAGE_FILES" | wc -l)
echo "Found $FILE_COUNT coverage file(s)"

# Generate HTML report
step "Generating HTML report"

# Check if ReportGenerator is available
if ! command -v reportgenerator &> /dev/null; then
    echo -e "${YELLOW}Installing ReportGenerator as global tool...${NC}"
    dotnet tool install --global dotnet-reportgenerator-globaltool 2>/dev/null || true
fi

# Build reports pattern
REPORTS_PATTERN=$(echo "$COVERAGE_FILES" | tr '\n' ';' | sed 's/;$//')

reportgenerator \
    "-reports:$REPORTS_PATTERN" \
    "-targetdir:$REPORT_DIR" \
    "-reporttypes:Html;TextSummary;Cobertura" \
    "-title:InControl Coverage Report"

if [ $? -eq 0 ]; then
    pass "Report generated at: $REPORT_DIR/index.html"
else
    fail "Report generation failed"
fi

# Display summary
step "Coverage Summary"
if [ -f "$REPORT_DIR/Summary.txt" ]; then
    cat "$REPORT_DIR/Summary.txt"
fi

# Threshold enforcement
if [ $THRESHOLD -gt 0 ]; then
    step "Threshold Enforcement"

    # Parse coverage from merged Cobertura file
    MERGED_COVERAGE="$REPORT_DIR/Cobertura.xml"
    if [ -f "$MERGED_COVERAGE" ]; then
        LINE_RATE=$(grep -oP 'line-rate="\K[^"]+' "$MERGED_COVERAGE" | head -1)
        LINE_PCT=$(echo "$LINE_RATE * 100" | bc -l | xargs printf "%.2f")

        echo "Current line coverage: $LINE_PCT%"
        echo "Required threshold:    $THRESHOLD%"

        if (( $(echo "$LINE_PCT < $THRESHOLD" | bc -l) )); then
            fail "Coverage $LINE_PCT% is below threshold $THRESHOLD%"
        else
            pass "Coverage meets threshold"
        fi
    fi
fi

# Open report
if [ "$OPEN_REPORT" = true ] && [ -f "$REPORT_DIR/index.html" ]; then
    if command -v xdg-open &> /dev/null; then
        xdg-open "$REPORT_DIR/index.html"
    elif command -v open &> /dev/null; then
        open "$REPORT_DIR/index.html"
    fi
fi

# Summary
echo ""
if [ $EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}  COVERAGE COMPLETE${NC}"
    echo -e "${GREEN}========================================${NC}"
else
    echo -e "${RED}========================================${NC}"
    echo -e "${RED}  COVERAGE FAILED${NC}"
    echo -e "${RED}========================================${NC}"
fi

exit $EXIT_CODE
