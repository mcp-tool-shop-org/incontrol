<p align="center">
  <a href="README.md">English</a> | <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/incontrol/readme.png" alt="InControl" width="400"></p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-9-purple?style=flat-square&logo=dotnet" alt=".NET 9">
  <img src="https://img.shields.io/badge/WinUI-3-blue?style=flat-square" alt="WinUI 3">
  <a href="https://github.com/mcp-tool-shop-org/incontrol/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/incontrol/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://codecov.io/gh/mcp-tool-shop-org/incontrol"><img src="https://codecov.io/gh/mcp-tool-shop-org/incontrol/branch/main/graph/badge.svg" alt="Coverage"></a>
  <a href="https://mcp-tool-shop-org.github.io/incontrol/"><img src="https://img.shields.io/badge/docs-handbook-blue?style=flat-square" alt="Handbook"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License"></a>
</p>

**Ollama chat for Windows.** On this PC by default. A GPU you rented, over SSH, when you say so.

InControl speaks the Ollama HTTP API. Nothing is sent to a rental until you connect one. The bar across the top then names that machine and says the chat leaves this PC.

## Why InControl?

- **This PC first.** Prompts stay here until you connect a rented GPU.
- **Same Ollama either way.** A rental runs Ollama on `127.0.0.1:11434`. InControl reaches it with an SSH local forward. The HTTP client never talks to a public port.
- **Not the tool allowlist.** Connectivity controls assistant tool URLs. It does not decide where the chat runs.
- **WinUI 3.** A Windows app, with markdown in the thread.
- **Assistant profiles** - Configurable personality, verbosity, and risk tolerance
- **Plugin system** - Extend functionality with sandboxed plugins and a manifest-based SDK
- **Policy engine** - Org/team/user policy layers govern tools, plugins, memory, and connectivity
- **Connectivity modes** - Offline-only, assisted, or connected with full audit logging

## NuGet Packages

The core libraries are available as standalone NuGet packages for building your own local AI integrations:

| Package | Version | Description |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | Domain models, conversation types, and shared abstractions for local AI chat applications. |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | LLM backend abstraction layer with streaming chat, model management, and health checks. Includes Ollama implementation. |

```bash
dotnet add package InControl.Core
dotnet add package InControl.Inference
```

```csharp
// Example: use InControl.Inference in your own app
var client = inferenceClientFactory.Create("ollama");
await foreach (var token in client.StreamChatAsync(messages))
{
    Console.Write(token);
}
```

## Target Hardware

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| GPU | RTX 3060 (8GB) | RTX 4080/5080 (16GB) |
| RAM | 16GB | 32GB |
| OS | Windows 10 1809+ | Windows 11 |
| .NET | 9.0 | 9.0 |

## Installation

Build from source. There is no MSIX release of this repository yet.

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build

# Run (Ollama on this PC)
dotnet run --project src/InControl.App
```

## Prerequisites

InControl requires a local LLM backend. We recommend [Ollama](https://ollama.ai/):

```bash
# Install Ollama from https://ollama.ai/download

# Pull a model
ollama pull llama3.2

# Start the server (runs on http://127.0.0.1:11434)
ollama serve
```

## A rented GPU

Settings → **Where this chat runs**. Paste the direct SSH command from the rental (`ssh -p <mapped-port> root@<public-ip> -i <key>`). The button says the chat will be sent to that machine.

The forward listens on `127.0.0.1:11436` on this PC, not on `11434`. The chat stays here until Ollama answers through that port.

Leave Ollama on `127.0.0.1:11434` on the rental. Do not set `OLLAMA_HOST=0.0.0.0`, and do not publish port 11434. The SSH port is the mapped sshd port, not 11434.

RunPod's `ssh.runpod.io` proxy is a shell only. It cannot forward a port. **Look up my RunPod pods** uses `RUNPOD_API_KEY` from the environment and fills the pod's direct public-IP SSH. The key is not stored. Lookup does not start a pod and does not send the chat. Vast documents the forward on the direct address. The address dies when the rental restarts. Look the pod up again, or paste the new command. The private key stays on this PC.

The full rules are in [docs/COMPUTE.md](docs/COMPUTE.md).

## Building

### Verify Build Environment

```powershell
# Run verification script
./scripts/verify.ps1
```

### Development Build

```bash
dotnet build
```

### Release Build

```powershell
# Creates release artifacts in artifacts/
./scripts/release.ps1
```

### Run Tests

```bash
dotnet test
```

## Architecture

InControl follows a clean, layered architecture:

```
+-------------------------------------------+
|         InControl.App (WinUI 3)           |  UI Layer
+-------------------------------------------+
|         InControl.ViewModels              |  Presentation
+-------------------------------------------+
|         InControl.Services                |  Business Logic
+-------------------------------------------+
|         InControl.Inference               |  LLM Backends
+-------------------------------------------+
|         InControl.Core                    |  Shared Types
+-------------------------------------------+
```

See [ARCHITECTURE.md](./docs/ARCHITECTURE.md) for detailed design documentation.

### Key subsystems

| Subsystem | Namespace | Purpose |
|-----------|-----------|---------|
| Assistant | `InControl.Core.Assistant` | Profiles, memory store, personality guard, onboarding |
| Plugins | `InControl.Core.Plugins` | Manifest-validated, sandboxed extensibility with SDK |
| Policy | `InControl.Core.Policy` | JSON policy documents (org/team/user), tool/plugin/memory/connectivity rules |
| Connectivity | `InControl.Core.Connectivity` | Three-mode network governance with audit trail |
| Health | `InControl.Services.Health` | Pluggable health checks (app, inference, storage) |
| Diagnostics | `InControl.Core.Diagnostics` | Support bundle creation with sanitized configs |

## Data Storage

All data is stored locally:

| Data | Location |
|------|----------|
| Sessions | `%LOCALAPPDATA%\InControl\sessions\` |
| Logs | `%LOCALAPPDATA%\InControl\logs\` |
| Cache | `%LOCALAPPDATA%\InControl\cache\` |
| Exports | `%USERPROFILE%\Documents\InControl\exports\` |

See [PRIVACY.md](./docs/PRIVACY.md) for complete data handling documentation.

## Troubleshooting

Common issues and solutions are documented in [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md).

### Quick Fixes

**App won't start:**
- Check that .NET 9.0 Runtime is installed
- Run `dotnet --list-runtimes` to verify

**No models available:**
- Ensure Ollama is running: `ollama serve`
- Pull a model: `ollama pull llama3.2`

**GPU not detected:**
- Update NVIDIA drivers to latest version
- Check CUDA toolkit installation

## Contributing

Contributions welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Write tests for new functionality
4. Submit a pull request

## Reporting Issues

1. Check [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md) first
2. Use the "Copy Diagnostics" feature in the app
3. Open an issue with diagnostics info attached

## Tech Stack

| Layer | Technology |
|-------|------------|
| UI Framework | WinUI 3 (Windows App SDK 1.6) |
| Architecture | MVVM with CommunityToolkit.Mvvm |
| LLM Integration | OllamaSharp, Microsoft.Extensions.AI |
| DI Container | Microsoft.Extensions.DependencyInjection |
| Configuration | Microsoft.Extensions.Configuration |
| Logging | Microsoft.Extensions.Logging + Serilog |

## Version

Current version: **2.0.0**. The package identity is `InControl.App` at `2.0.0.0`, because Partner Center already has this app through 1.4.0. The name on the repo stays InControl.

See [CHANGELOG.md](./CHANGELOG.md) for why the version jumped, and for the 0.3.0 history.

## Security & Data Scope

InControl is a WinUI 3 chat application for Ollama.

- **Data accessed:** Ollama on this PC, chat history in local storage, and, only after you connect one, Ollama on a machine you reach over SSH
- **Data not accessed:** No InControl account, no telemetry, no analytics
- **Permissions:** Loopback HTTP to Ollama, an SSH client on this PC when you connect a rental, and the file system for chat history

Full policy: [SECURITY.md](SECURITY.md)

---

## Support

- **Bug reports:** [Issues](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **Security:** [SECURITY.md](SECURITY.md)

## License

[MIT](LICENSE) -- see [LICENSE](LICENSE) for full text.

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
