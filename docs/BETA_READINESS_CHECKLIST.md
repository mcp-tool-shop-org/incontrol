# Public Beta Readiness Checklist

> **Version**: 0.9.0-rc.1
> **Target Date**: 2026-02-03
> **Status**: Ready for RC1 Cut

## Pre-Release Verification

### 1. Build Quality

| Check | Status | Notes |
|-------|--------|-------|
| Solution builds without errors | Pass | All projects compile |
| Solution builds without warnings | Pass | `dotnet build -warnaserror` passes |
| All tests pass | Pass | `dotnet test` succeeds |
| Code formatting clean | Pass | `dotnet format --verify-no-changes` |

### 2. Documentation

| Document | Status | Location |
|----------|--------|----------|
| README with quick start | Done | `README.md` |
| Installation runbook | Done | `docs/INSTALL_RUNBOOK.md` |
| Release process guide | Done | `docs/RELEASE_PROCESS.md` |
| Button coverage matrix | Done | `docs/BUTTON_COVERAGE_MATRIX.md` |
| Theme audit | Done | `docs/THEME_AUDIT.md` |
| Phase 12 audit trail | Done | `docs/PHASE12_AUDIT.md` |
| Changelog | Done | `CHANGELOG.md` |

### 3. GitHub Repository

| Item | Status | Notes |
|------|--------|-------|
| Repo exists in org | Done | `mcp-tool-shop-org/InControl-Desktop` |
| SECURITY.md | Done | Vulnerability reporting |
| CODE_OF_CONDUCT.md | Done | Contributor Covenant |
| Issue templates | Done | Bug, Feature, Question |
| PR template | Done | With checklist |
| CI workflow | Done | Build, test, lint |
| Release workflow | Done | Signed MSIX generation |

### 4. Application Features

| Feature | Status | Evidence |
|---------|--------|----------|
| Local inference (Ollama) | Done | Model Manager integration |
| Session management | Done | Sidebar with history |
| Theme support (Light/Dark) | Done | Theme audit passed |
| Crash recovery | Done | CrashRecoveryService |
| Diagnostics | Done | DiagnosticsService |
| Help center | Done | Troubleshooting assistant |
| Policy controls | Done | PolicyPage |
| Connectivity settings | Done | ConnectivityPage |
| Extensions framework | Done | ExtensionsPage |
| Command palette | Done | Ctrl+K shortcut |

### 5. Quality Assurance

| Test Category | Status | Evidence |
|---------------|--------|----------|
| Button coverage tests | Done | 58 controls documented |
| Soak test harness | Done | 2-hour test available |
| UI smoke tests | Done | InControl.UITests project |
| Theme consistency | Done | Audit passed |

### 6. CI/CD Pipeline

| Pipeline | Status | Triggers |
|----------|--------|----------|
| CI (build + test) | Done | Push/PR to main, develop |
| Proof pack generation | Done | Push to main |
| Signed release | Done | Push v* tag |

### 7. Security

| Item | Status | Notes |
|------|--------|-------|
| No hardcoded secrets | Pass | Checked |
| HTTPS for external calls | Pass | Ollama API only |
| Secure storage (DPAPI) | N/A | Local-only app |
| Input validation | Pass | Model names validated |

## Release Artifacts

### Required for RC1

- [ ] MSIX package (signed)
- [ ] Checksums file (SHA256)
- [ ] Signature info JSON
- [ ] Proof pack ZIP
- [ ] Release notes

### File Naming Convention

```
InControl-Desktop-0.9.0-rc.1.msix
checksums-0.9.0-rc.1.txt
signature-info-0.9.0-rc.1.json
proof-pack-0.9.0-rc.1.zip
```

## Release Process

### Steps to Cut RC1

1. **Final verification**
   ```bash
   dotnet build -c Release -p:Platform=x64
   dotnet test
   ```

2. **Update version** (already set to 0.9.0-rc.1)
   - `src/InControl.App/InControl.App.csproj`
   - `src/InControl.App/Package.appxmanifest`

3. **Update CHANGELOG.md** (already done)
   - Add release date
   - Verify all changes documented

4. **Create and push tag**
   ```bash
   git tag -a v0.9.0-rc.1 -m "Release Candidate 1 for public beta"
   git push origin v0.9.0-rc.1
   ```

5. **Monitor CI**
   - Signed release workflow triggers
   - MSIX is created and signed
   - Artifacts uploaded to release

6. **Verify release**
   - Download MSIX
   - Verify checksum
   - Install on clean machine
   - Run basic smoke test

7. **Publish release**
   - Edit draft release on GitHub
   - Add release notes
   - Publish

## Known Limitations for Beta

| Limitation | Workaround | Fix Timeline |
|------------|------------|--------------|
| Windows only | N/A | Future |
| Requires Ollama installed | Link to ollama.com | N/A |
| No cloud sync | Local only by design | N/A |
| No auto-updates | Manual update via Store | Post-GA |

## Feedback Channels

- GitHub Issues: Bug reports and feature requests
- Discussions: Questions and community chat

## Rollback Plan

If critical issues discovered:

1. Yank the release (mark as prerelease, add warning)
2. Communicate via GitHub Discussions
3. Cut hotfix release (0.9.1-rc.1)

## Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Developer | | | |
| QA | | | |
| Release Manager | | | |

---

## Appendix: Phase 12 Commit Summary

| Commit | Description | Status |
|--------|-------------|--------|
| 01 | GitHub Repo + Project Hygiene | Done |
| 02 | RC Versioning + Release Notes | Done |
| 03 | Cold Machine Install Certification | Done |
| 04 | 2-Hour Soak Test Harness | Done |
| 05 | Crash Reporting + Recovery UX | Done |
| 06 | Button Coverage Tests | Done |
| 07 | Help Center Upgrade | Done |
| 08 | Theme Consistency Audit | Done |
| 09 | Release Artifact Proof Pack | Done |
| 10 | RC1 Cut + Beta Readiness Gate | Done |

**All 10 commits complete. Ready for RC1.**
