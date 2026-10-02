---
title: Configuration
description: Settings, backends, data storage, policies, and troubleshooting.
sidebar:
  order: 3
---

## Configuration sections

InControl uses `Microsoft.Extensions.Configuration` with JSON settings files. The main configuration sections map to strongly-typed options classes.

### App options

Section: `App`

| Setting | Default | Description |
|---------|---------|-------------|
| `Theme` | `"System"` | Application theme: System, Light, or Dark |
| `ShowTrayIcon` | `true` | Show the system tray icon |
| `MinimizeToTray` | `true` | Minimize to tray instead of closing |
| `StartMinimized` | `false` | Start the app minimized |
| `CheckForUpdates` | `true` | Check for updates on startup |
| `DataPath` | `%LOCALAPPDATA%/InControl/data` | Unused unless a caller is added. Conversations are `%LOCALAPPDATA%\InControl\sessions` |

### Inference options

Section: `Inference`

| Setting | Default | Description |
|---------|---------|-------------|
| `Backend` | `"Ollama"` | Backend to use: Ollama only |
| `DefaultModel` | `"llama3.2"` | Model to use when none is specified |
| `DefaultTemperature` | `0.7` | Generation temperature |
| `DefaultMaxTokens` | `2048` | Maximum tokens to generate |
| `TimeoutSeconds` | `300` | Request timeout |
| `RetryCount` | `3` | Number of retry attempts for failed requests |
| `RetryDelayMs` | `1000` | Delay between retries in milliseconds |

### Ollama options

Section: `Inference:Ollama`

| Setting | Default | Description |
|---------|---------|-------------|
| `BaseUrl` | `http://127.0.0.1:11434` | Ollama API endpoint |
| `KeepAlive` | `true` | Keep models loaded between requests |
| `KeepAliveMinutes` | `5` | How long to keep models loaded (0 = until unload) |
| `NumGpuLayers` | `-1` | GPU layers to offload (-1 = all, 0 = CPU only) |
| `ContextSize` | `8192` | Context window size in tokens |
| `NumThreads` | `null` | CPU threads for inference (null = auto) |

## Policy system

InControl supports layered JSON policy documents for organizational governance. Policies control tool usage, plugin loading, memory retention, connectivity modes, and update channels.

### Policy file locations

| Level | Windows path |
|-------|-------------|
| Organization | `%ProgramData%\InControl\policy.json` |
| Team | `%ProgramData%\InControl\team-policy.json` |
| User | `%LOCALAPPDATA%\InControl\user-policy.json` |

Organization policies take highest precedence and can be locked to prevent user overrides.

### Policy categories

- **Tools** -- allow/deny/require-approval lists with optional constraints and time-based conditions
- **Plugins** -- enable/disable, max risk level, trusted authors, per-plugin allow/deny
- **Memory** -- max retention days, max memory count, encrypt-at-rest, auto-formation toggle
- **Connectivity** -- allowed modes, default mode, allowed/blocked domains
- **Updates** -- auto-update toggle, allowed channels (stable/beta/dev/canary), minimum version

## The offline switch

Offline is off when the app is installed. Turning it on refuses a rented GPU, a RunPod lookup, and a model download. Chat on this PC still works. The switch does not block web search, app updates, or extension network. It is not a kill switch for every socket, and it is not the default.

Tool-URL allowlisting is a different control. See the repository file `docs/CONNECTIVITY.md`.

## Data storage

All data is stored locally on your machine:

| Data | Location |
|------|----------|
| Sessions | `%LOCALAPPDATA%\InControl\sessions\` |
| Logs | `%LOCALAPPDATA%\InControl\logs\` |
| Cache | `%LOCALAPPDATA%\InControl\cache\` |
| Exports | `%USERPROFILE%\Documents\InControl\exports\` |
| Support bundles | `%LOCALAPPDATA%\InControl\support\` |

## Target hardware

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| GPU | RTX 3060 (8GB) | RTX 4080/5080 (16GB) |
| RAM | 16GB | 32GB |
| OS | Windows 10 1809+ | Windows 11 |
| .NET | 9.0 | 9.0 |

## Troubleshooting

**App won't start:**
- Check that .NET 9.0 Runtime is installed: `dotnet --list-runtimes`

**No models available:**
- Ensure Ollama is running: `ollama serve`
- Pull a model: `ollama pull llama3.2`

**GPU not detected:**
- Update NVIDIA drivers to the latest version
- Check CUDA toolkit installation

**Health check failures:**
- Use the "Copy Diagnostics" feature in the app to create a support bundle
- Support bundles include logs, health reports, and sanitized config (never conversation content)

For the rental path, see [Where the chat runs](/incontrol/handbook/where-it-runs/). Support notes live in the repository `SUPPORT.md`.
