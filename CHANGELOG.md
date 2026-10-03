# Changelog

All notable changes to InControl will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

The app version is **2.0.0**. The Microsoft Store already has this product, Store ID 9N1FG39JWF83, through package 1.4.0.0. An upload is accepted only when its identity matches the Product Identity page in Partner Center: Name `mcp-tool-shop.InControl-Desktop`, Publisher `CN=5305D976-6952-4F00-9C21-3A5DB090359F`, PublisherDisplayName `mcp-tool-shop`, and a higher version. The MSIX version is **2.0.0.0**. The name in the window and on the Start tile stays InControl. This is not a rewrite, and it is not a retag of 0.3.0. `InControl.Core` stays 1.2.2 and `InControl.Inference` stays 1.0.2. Those NuGet packages are not republished. No MSIX file is committed in this repository.

## [Unreleased]

### Changed
- The app version is 2.0.0, and the MSIX identity version is 2.0.0.0. The package identity is `mcp-tool-shop.InControl-Desktop`, publisher display name `mcp-tool-shop`, as on the Partner Center Product Identity page. An earlier draft used `InControl.App`, which was the 1.4.0 upload's file name and would have been rejected. The application id is `App` again, as in 1.3.0, so Start and taskbar pins survive the update. NuGet library versions are unchanged.
- The package runs on Windows 10 version 2004 and later. The build had been writing Windows 11 22H2 as the minimum.
- The tiles, taskbar icon, Store logo and splash screen are the llama from the README. `scripts/generate-icons.py` draws them from `logo.png`.
- PRIVACY.md lists every network connection, including the one-time voice model download and the diagnostics check, says that project instructions and remembered notes go to a connected rental, and describes Store storage and updates.

### Fixed
- The status strip says Online when Ollama answers and Offline when it does not. It had the two the wrong way round.
- A long reply or model pull is no longer cut off at 100 seconds. The inference timeout is now how long a stream may go quiet, not how long it may run.
- Changing the endpoint no longer drops a reply that is still streaming. The old connection is closed after its last call ends.
- `localhost` in the Ollama address is used as 127.0.0.1, so an IPv4-only Ollama is found.
- Session, project and note files are written to a temporary file and then swapped in, so a crash during a save cannot leave half a file.
- A session deleted while its reply is being saved stays deleted. A session whose file cannot be deleted stays in the list with its notes, and the window says so.
- When a chat cannot be saved, the window says so instead of only logging it. Failures to load, rename, delete, remember, export or speak are shown too.
- A cancelled load is no longer reported as corrupt session files, and the next load tries again.
- Stop never throws when a reply is ending at the same moment.
- Switching sessions, renaming, or starting a new session while a reply is streaming keeps the reply running. Going back to that session shows the reply so far and the Cancel button.
- Remember for this session files the note under that session's project. Right-clicking a row no longer changes which session the memory list shows.
- Duplicate stores a real copy with its messages and project. Before, the copy existed only in the sidebar.
- Model Manager keeps the selected default model when it refreshes.
- Connect, Stay on this PC, RunPod lookup and the Help page diagnostics handlers catch their errors, and an unhandled UI exception no longer closes the app.
- After an unclean exit, the next launch says so and that saved chats are intact. Code that claimed to recover an unsent prompt, but never did, is removed.
- Clear All Memory clears the remembered notes. Controls that did nothing are removed or relabelled: Change Storage, Settings Export Diagnostics, Clear activity, plugin install, policy Configure and audit Export, and the composer's Attach file. Reset to Defaults is now Reset appearance.
- A message card stops listening to a message it no longer shows, so tokens cannot paint into the wrong card.
- Restoring a backup extracts it to a staging folder first. A corrupt or empty backup no longer deletes the current sessions.
- Exporting all sessions reads the list once. Clearing session notes clears only that session's notes.
- Voice works from the Store package. The voices and eSpeak data now ship inside it, the app starts in its own folder instead of System32, and the voice model downloads into the app cache instead of beside the read-only exe.
- The SSH client config no longer sets `ClearAllForwardings`. That keyword was erasing `LocalForward` after OpenSSH parsed the file, so `ssh -N` could sit with nothing forwarded.
- When that SSH process exits, the chat comes back to this PC and the banner says so. While the tunnel is up, the banner includes the Ollama version the probe already read.
- Model Manager and diagnostics talk to the endpoint the banner is naming, not always `localhost:11434`.
- Offline mode refuses a rented GPU, RunPod lookup, and a model download, and it closes a rental that is already open. The switch no longer says every network connection is blocked. Chat on this PC still works.

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
