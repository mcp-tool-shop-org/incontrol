# Release Notes

The published app release is **0.3.0**. It is the source tree at tag `v0.3.0` in [mcp-tool-shop-org/incontrol](https://github.com/mcp-tool-shop-org/incontrol). There is no MSIX. Install by building from source, as `docs/INSTALLATION.md` describes. The notes for that release are in `CHANGELOG.md`.

The 0.1.0 section below was written for a release that was not published. Do not download an InControl-Desktop package. Nothing in this file is an installer.

---

## Version 0.1.0 - unpublished draft

**Release Date:** not released

### Overview

Draft notes for a Windows chat app. They are not the 0.3.0 release.

### Highlights

- **This PC first**: The chat stays on this device until you connect a rented GPU over SSH.
- **Offline switch**: It refuses a rented GPU, RunPod lookup, and model download. It does not block every network connection. Chat on this PC still works.
- **Operator Control**: You decide when the chat leaves this PC.

### Features

#### Chat Interface
- Real-time streaming responses from local LLMs
- Multi-session conversation management
- Session history and search
- Message editing and regeneration

#### Assistant System
- Personal assistant with memory capabilities
- Tool execution framework with approval controls
- Reasoning trace for explainability
- Error recovery with user guidance

#### Connectivity
- Offline-only mode (default)
- Assisted mode (approved operations only)
- Connected mode (full network with audit)
- Per-endpoint permission rules
- Complete network activity audit log

#### Settings
- Theme customization (Light/Dark/System)
- Ollama connection configuration
- Update preferences
- Connectivity controls

### System Requirements

- Windows 10 version 1903 or later
- Windows 11 (any version)
- .NET 9.0 Runtime (bundled)
- 4 GB RAM minimum (8 GB recommended)
- 100 MB disk space (plus Ollama models)

### Known Limitations

- Windows-only in this release
- Requires Ollama for LLM inference
- Memory system uses local storage only

### Upgrade Path

This is the initial release. Future upgrades will preserve:
- Conversation history
- Memory items
- Settings (with migration if needed)

---

## Release Notes Template

For future releases, use this template:

```markdown
## Version X.Y.Z - Title

**Release Date:** YYYY-MM-DD

### Overview
Brief description of this release.

### Breaking Changes
- List any breaking changes
- Include migration instructions

### New Features
- Feature 1: Description
- Feature 2: Description

### Improvements
- Improvement 1
- Improvement 2

### Bug Fixes
- Fix 1: Description (#issue-number)
- Fix 2: Description (#issue-number)

### Security
- Security fix 1
- Security improvement 2

### Known Issues
- Issue 1 (workaround: ...)
- Issue 2

### Dependencies
- Updated: dependency@version
- Added: new-dependency@version

### Upgrade Notes
Step-by-step upgrade instructions if needed.
```

---

## Version History

| Version | Date | Highlights |
|---------|------|------------|
| 0.1.0 | TBD | Initial release |

---

## Support Lifecycle

- **Current release**: Full support
- **Previous minor version**: Security fixes only
- **Older versions**: No active support

See [SUPPORT.md](../SUPPORT.md) for help resources.
