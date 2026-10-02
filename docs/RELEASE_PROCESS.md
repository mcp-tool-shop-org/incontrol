# Release Process

## Version Scheme

InControl follows [Semantic Versioning](https://semver.org/) with Release Candidate (RC) designations:

```
MAJOR.MINOR.PATCH[-rc.N]
```

### Version Types

| Type | Format | Example | Use Case |
|------|--------|---------|----------|
| Release Candidate | `X.Y.Z-rc.N` | `0.9.0-rc.1` | Pre-release testing |
| Stable Release | `X.Y.Z` | `1.0.0` | Production-ready |
| Patch Release | `X.Y.Z` | `1.0.1` | Bug fixes only |

### Version Bumping Rules

- **MAJOR**: Breaking changes, significant rewrites
- **MINOR**: New features, backward-compatible
- **PATCH**: Bug fixes, security patches
- **RC**: Pre-release iteration (`-rc.1`, `-rc.2`, etc.)

## Release Workflow

### 1. Pre-Release (RC)

```bash
# Update version in .csproj
# Version: 0.9.0-rc.1

# Update CHANGELOG.md
# Tag the release
git tag -a v0.9.0-rc.1 -m "Release Candidate 0.9.0-rc.1"
git push origin v0.9.0-rc.1
```

### 2. Changelog Requirements

Every release MUST update `CHANGELOG.md` with:

```markdown
## [0.9.0-rc.1] - 2026-02-03

### Added
- New feature descriptions

### Changed
- Behavior changes

### Fixed
- Bug fixes with issue references

### Security
- Security patches (if any)

### Known Issues
- Any known limitations
```

### 3. Release Artifacts

Each release produces:

| Artifact | Description |
|----------|-------------|
| `InControl.App_X.Y.Z_x64.msix` | Signed Windows package |
| `InControl.App_X.Y.Z_x64.msix.sha256` | Checksum file |
| `CHANGELOG.md` | Release notes |
| `support-bundle-sample.zip` | Sanitized support bundle example |

### 4. Release Notes Template

```markdown
# InControl v0.9.0-rc.1

> Release Candidate - Not for production use

## Highlights
- Brief summary of major changes

## What's New
- Detailed feature list

## Bug Fixes
- Fixed issue descriptions

## Breaking Changes
- Any migration steps needed

## Known Issues
- Current limitations

## Installation
1. Download the MSIX package
2. Verify checksum: `certutil -hashfile InControl.App_0.9.0-rc.1_x64.msix SHA256`
3. Double-click to install or use PowerShell: `Add-AppPackage -Path "..."`

## Feedback
Report issues: https://github.com/mcp-tool-shop-org/InControl-Desktop/issues
```

## CI/CD Integration

### GitHub Actions Workflow

On tag push (`v*`):
1. Build Release configuration
2. Sign MSIX package
3. Generate checksums
4. Extract changelog section for release notes
5. Create GitHub Release with artifacts

### Artifact Naming Convention

```
InControl.App_{version}_{arch}[_Test].msix
```

Examples:
- `InControl.App_0.9.0-rc.1_x64.msix` - Release candidate
- `InControl.App_1.0.0_x64.msix` - Stable release
- `InControl.App_0.9.0-rc.1_x64_Test.msix` - Unsigned test build

## Hotfix Process

For critical security or stability fixes:

1. Branch from the release tag: `git checkout -b hotfix/v0.9.1 v0.9.0`
2. Apply minimal fix
3. Bump patch version
4. Update CHANGELOG.md
5. Tag and release
6. Merge back to main

## Rollback Procedure

If a release has critical issues:

1. Mark the GitHub release as "Pre-release"
2. Update release notes with warning
3. Direct users to previous version download
4. Create hotfix or roll forward

## Release Checklist

### Pre-Release
- [ ] All tests pass
- [ ] CHANGELOG.md updated
- [ ] Version bumped in .csproj
- [ ] Phase 12 audit complete (for major releases)
- [ ] Support bundle export works

### Release
- [ ] Tag created and pushed
- [ ] CI build succeeds
- [ ] Artifacts uploaded
- [ ] Release notes published
- [ ] Announcement posted (if applicable)

### Post-Release
- [ ] Monitor for crash reports
- [ ] Check download metrics
- [ ] Respond to early feedback
- [ ] Update documentation if needed
