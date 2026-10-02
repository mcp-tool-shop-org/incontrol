# Scorecard

**Repo:** incontrol
**Date:** 2026-10-02

The February 2026 card below scored the dormant prototype. It is history. The 2026-10-02 ship gate is the current list: app version 2.0.0, prepared MSIX identity InControl.App 2.0.0.0, no MSIX file in the repo. Tag v0.3.0 is an older source-only release. NuGet library versions stay InControl.Core 1.2.2 and InControl.Inference 1.0.2.

**Date:** 2026-02-27
**Type tags:** `[desktop]`

## Pre-Remediation Assessment

| Category | Score | Notes |
|----------|-------|-------|
| A. Security | 7/10 | SECURITY.md outdated (v0.9.x, wrong email), no threat model in README |
| B. Error Handling | 9/10 | Good error handling, Serilog integration |
| C. Operator Docs | 8/10 | Excellent CHANGELOG, README thorough, version display outdated |
| D. Shipping Hygiene | 9/10 | CI, build, extensive architecture |
| E. Identity (soft) | 10/10 | Logo, translations, landing page, metadata all present |
| **Overall** | **43/50** | |

## Key Gaps

1. SECURITY.md referenced v0.9.x with wrong email — needed v1.3.x with data scope
2. README version display showed 0.4.0-alpha — outdated
3. README missing Security & Data Scope section

## Post-Remediation

| Category | Before | After |
|----------|--------|-------|
| A. Security | 7/10 | 10/10 |
| B. Error Handling | 9/10 | 10/10 |
| C. Operator Docs | 8/10 | 10/10 |
| D. Shipping Hygiene | 9/10 | 10/10 |
| E. Identity (soft) | 10/10 | 10/10 |
| **Overall** | 43/50 | 50/50 |
