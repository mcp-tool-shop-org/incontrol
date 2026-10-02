# Changelog

All notable changes to InControl will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

The app version is **0.3.0**. That number is lower than the old library packages on purpose. The dormant prototype had already published `InControl.Core` 1.2.2 and `InControl.Inference` 1.0.2 to NuGet, and those package versions stay where they are. This repository does not republish them. 0.3.0 is not a 1.0 product.

## [0.3.0] - 2026-10-02

### Added
- The rental forward listens on `127.0.0.1:11436`. It refuses port 11434, so a dead tunnel cannot be answered by Ollama on this PC. The chat stays local until `/api/version` answers through the forward.
- **Look up my RunPod pods** reads `RUNPOD_API_KEY` from the environment and fills the direct SSH command for a pod that is already running. The key is not stored. Lookup does not start a pod or send the chat. A pod that publishes port 11434 is refused.
- Codecov on pull requests. The failing bar is patch coverage at 90%. The repository-wide percentage is informational.

### Changed
- There is still no MSIX. The GitHub release is the source tree. A tag runs the library tests and does not attach an installer.
- NuGet publish no longer runs when a GitHub release is published.

## [0.2.0] - 2026-10-02

### Added
- Graduated the dormant InControl Desktop prototype into the `incontrol` repository.
- Ollama stays the HTTP client. A rented GPU is an SSH local forward to `127.0.0.1:11434` on that machine.
- RunPod's `ssh.runpod.io` proxy is refused, because that path cannot forward a port. Direct SSH (public IP and mapped port) is the dial, for RunPod, Vast, or any other sshd.
- The chat shows a persistent line naming the remote machine while prompts leave this PC. The tool-URL allowlist is unchanged and does not cover that choice.

### Changed
- The public copy no longer says conversations never leave the machine. They stay here until you connect a rental.

## [1.3.2] - 2026-03-25

### Added
- Ollama connectivity check in `DiagnosticsInfo.CheckOllamaAsync()` — lightweight HTTP probe without OllamaSharp dependency
- 3 new diagnostics tests (1349 total)

### Fixed
- Directory.Build.props version stuck at 0.4.0-alpha — updated to 1.3.2
- App InformationalVersion mismatch (was 1.3.0, now 1.3.2)
- All version fields aligned across solution

## [1.3.1] - 2026-02-27

### Added
- Shipcheck compliance: SHIP_GATE.md, SCORECARD.md
- Updated SECURITY.md with current version and data scope
- Security & Data Scope section in README

## [1.3.0]

### Changed
- App version bump to 1.3.0

---

## [1.2.0] - 2026-02-12

### InControl.Core

#### Added
- **`IAsyncDisposable` on `PluginHost`** — new `DisposeAsync()` properly awaits async plugin disposal; sync `Dispose()` retained for backward compatibility
- **`TryProposeAction` on `ToolApprovalManager`** — `Result<ToolProposal>`-returning alternative to the throwing `ProposeAction`, unifies the error-handling pattern
- **`IDataPathsProvider` interface + `DataPathsProvider`** — DI-friendly path resolution; register as `services.AddSingleton<IDataPathsProvider, DataPathsProvider>()`
- **`DataPaths.Configure(DataPathsConfig)`** — override default paths for testing and custom deployments; `ResetConfiguration()` for test cleanup
- **`Result<T>.Unwrap()`** — non-nullable value accessor for value-type `T` where `MemberNotNullWhen` cannot narrow `T?` to `T`

#### Fixed
- **`PluginHost.Dispose()` bare catch** narrowed to `catch (Exception)` for explicit best-effort semantics
- **`DataPaths` internal routing** now uses `CurrentConfig` indirection, enabling `Configure()` overrides to take effect on all path properties and methods

---

## [1.1.0] - 2026-02-12

### InControl.Core

#### Fixed
- **ConfigureAwait(false)** added to all async methods across 9 files — prevents potential UI deadlocks when Core is consumed by WinUI/WPF callers
- **DiagnosticsReport.ToJson()** now reuses `StateSerializer` options instead of allocating new `JsonSerializerOptions` per call
- **ToolRegistry audit log** capped at 10,000 entries (trims oldest 1,000) — prevents unbounded memory growth in long-running sessions
- **PolicyEngine regex caching** — glob-to-regex patterns are compiled once and cached in a `ConcurrentDictionary`, eliminating redundant `Regex.Escape` + `Regex.IsMatch` on every policy evaluation
- **DataPaths.ClearTemp / GetDirectorySize** bare `catch {}` narrowed to `catch (IOException)` — no longer silently swallows `OutOfMemoryException`, `ThreadAbortException`, etc.
- **SecurityConfig** modernized from `Array.Empty<string>()` to `[]` collection expressions

---

## [0.9.0-rc.1] - 2026-02-03

> **Release Candidate** - Pre-release for testing. Not recommended for production use.

### Highlights
- Full Ollama integration for local AI model management
- Complete UI framework with 15+ pages and controls
- Phase 12 release candidate preparation

### Added
- **Ollama Integration**: Direct connection to local Ollama instance
  - Live model list with metadata (size, family, parameters)
  - Pull models directly from Ollama library
  - Quick-pull buttons for popular models (llama3.2, mistral, codegemma)
  - Connection status indicator with version display
- **Personal Assistant System**: Local-first assistant with context memory
- **Conversation Management**: Multi-session support with sidebar navigation
- **Memory System**: Persistent context across sessions
- **Tool Execution Framework**: With approval controls and sandboxing
- **Offline-First Architecture**: Complete air-gap capability
- **Update Management**: Operator-controlled update policies
- **Audit Logging**: Full network activity tracking
- **Command Palette**: Ctrl+K quick access to all functions
- **Inspector Panel**: Real-time inference statistics
- **Support Bundle**: One-click diagnostic export

### Changed
- Welcome text updated from "RTX GPU" to "Ollama-powered"
- Model Manager redesigned for Ollama-native workflow
- Improved theme resource organization

### Fixed
- Resource dictionary not merged causing page crashes
- DateTime nullable handling in model info display

### Security
- All network access disabled by default (OfflineOnly mode)
- Explicit operator approval required for internet connectivity
- Per-endpoint permission rules
- Complete audit trail for network requests

### Known Issues
- Branch protection requires manual GitHub admin configuration
- MSIX signing requires certificate setup for production

---

## [0.1.0] - 2026-01-15 - Initial Release

### Added
- Core chat interface with streaming responses
- Ollama integration for local LLM inference
- Session management and persistence
- Theme support (Light, Dark, System)
- Tray icon with minimize to tray option
- Basic settings management

### Technical
- Built on .NET 9.0 and WinUI 3
- MVVM architecture with CommunityToolkit
- Local SQLite storage for conversations
- RTX GPU acceleration support

---

## Version Numbering

- **MAJOR**: Breaking changes to user data or settings
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, no feature changes

## Upgrade Notes

When upgrading between versions:

1. **Backup your data** before upgrading (Settings → Export)
2. Review the changelog for any migration notes
3. Check the [Release Notes](./docs/RELEASE_NOTES.md) for version-specific details

## Reporting Issues

Found a bug? Please report it:
1. Check if the issue already exists in [Issues](../../issues)
2. Create a new issue with steps to reproduce
3. Include your version number and system info
