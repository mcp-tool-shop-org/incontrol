# Release Process

## What 0.3.0 actually shipped

Tag `v0.3.0` in [mcp-tool-shop-org/incontrol](https://github.com/mcp-tool-shop-org/incontrol) is the source tree. The GitHub release does not attach an MSIX. A tag runs the library tests. It does not sign a package, and it does not publish `InControl-Desktop`.

To install that release, build from source. See `docs/INSTALLATION.md`.

Issues: https://github.com/mcp-tool-shop-org/incontrol/issues

The sections below still describe an older release-candidate plan (0.9.0-rc.1, a signed MSIX, the InControl-Desktop repository name). That plan is not the current release path. Do not cut a release by following it.

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
| Git tag `v*` | The source tree. This is the 0.3.0 release artifact. |
| `CHANGELOG.md` | Release notes |

A signed MSIX is not produced. Do not publish `InControl-Desktop-x.y.z.msix` or `InControl.App_X.Y.Z_x64.msix` as the release.

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
Build from source. There is no MSIX. See docs/INSTALLATION.md.

## Feedback
Report issues: https://github.com/mcp-tool-shop-org/incontrol/issues
```

## CI/CD Integration

### GitHub Actions Workflow

On tag push (`v*`):
1. Test the libraries
2. The workflow does not build or sign an MSIX
3. Release notes live in CHANGELOG.md

There is no `InControl.App_{version}_{arch}.msix` artifact. The 0.3.0 release is the git tag.

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
