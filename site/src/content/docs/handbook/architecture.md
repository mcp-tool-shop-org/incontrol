---
title: Architecture
description: Layered design, subsystems, and tech stack.
sidebar:
  order: 2
---

InControl follows a clean, layered architecture with strict one-way dependencies.

## Layer diagram

```
InControl.App (WinUI 3)           UI Layer
InControl.ViewModels               Presentation (MVVM)
InControl.Services                 Business Logic
InControl.Inference                LLM Backends
InControl.Core                     Shared Types & Domain Models
```

Each layer depends only on the layers below it. The UI layer never touches Inference directly; it goes through ViewModels and Services.

## Tech stack

| Layer | Technology |
|-------|------------|
| UI Framework | WinUI 3 (Windows App SDK 1.6) |
| Architecture | MVVM with CommunityToolkit.Mvvm |
| LLM Integration | OllamaSharp, Microsoft.Extensions.AI |
| DI Container | Microsoft.Extensions.DependencyInjection |
| Configuration | Microsoft.Extensions.Configuration |
| Logging | Microsoft.Extensions.Logging + Serilog |

## Key subsystems

### Assistant

The assistant subsystem (`InControl.Core.Assistant`) manages personality and behavioral configuration.

- **AssistantProfile** -- immutable record with Tone, Verbosity, ExplanationLevel, and RiskTolerance settings. Ships with three presets: Default (professional/concise), Minimal (brief, low caution), and Detailed (proactive explanation, high caution).
- **AssistantMemory** -- local, scoped, auditable memory store. Each memory item has a type (Preference, Fact, Instruction, Entity, Decision), scope (Session, User, Global), and source (ExplicitUser, Inferred, System). Supports export to JSON.
- **PersonalityGuard** -- prevents runtime mutation of personality by the assistant itself.
- **InternetTool** -- internet access is exposed as a governed tool, not a capability. The assistant must declare the endpoint, purpose, expected data, and retention policy before each request.

### Plugins

The plugin subsystem (`InControl.Core.Plugins`) provides sandboxed extensibility.

- **PluginManifest** -- JSON manifest declaring ID, version, author, permissions, capabilities, risk level, and network intent. Validated by `ManifestValidator` before loading.
- **PluginSdk** -- `PluginBase` abstract class with lifecycle management (initialize, execute, dispose). Provides file, network, memory, and storage APIs scoped to declared permissions.
- **PluginSandbox** -- enforces permission boundaries at runtime.
- **PluginAuditLog** -- records all plugin actions for operator review.
- **Risk levels** -- ReadOnly (level 1), LocalMutation (level 2), Network (level 3), SystemAdjacent (level 4, reserved).

### Policy engine

The policy engine (`InControl.Core.Policy`) provides layered governance through JSON policy documents.

- **PolicyDocument** -- versioned schema covering tools, plugins, memory, connectivity, and updates.
- **Source precedence** -- Organization > Team > User > Session > Default. Organization policies can be locked to prevent user overrides.
- **Decisions** -- Allow, Deny, AllowWithApproval, AllowWithConstraints. Every decision includes a human-readable reason.
- **PolicyViewer** -- read-only UI showing effective policy state and audit trail.
- **PolicyDiagnostics** -- reports which rules matched and why.

### Connectivity

See the repository file `docs/CONNECTIVITY.md`. The offline switch is off when the app is installed. Turning it on refuses a rented GPU, a RunPod lookup, and a model download. Chat on this PC still works. The switch does not block web search, app updates, or extension network. It is not a kill switch for every socket, and it is not the default.

Tool-URL allowlisting is a different control. It does not decide that prompts stay on this PC.

### Health checks

The health subsystem (`InControl.Services.Health`) runs pluggable checks:

- **AppHealthCheck** -- application state
- **InferenceHealthCheck** -- backend connection and model availability
- **StorageHealthCheck** -- local file system access

Results are aggregated into a `HealthReport` with per-check timings.

### Diagnostics

The diagnostics subsystem (`InControl.Core.Diagnostics`) creates support bundles:

- Diagnostics report (system info)
- Health report snapshot
- Recent log files (up to 5)
- Sanitized configuration (never includes secrets)
- Optional session metadata (file sizes/dates only, never conversation content)

### Onboarding

The `OnboardingViewModel` guides first-run users through four steps: Welcome, Backend Check, Model Selection, and Ready. Users can skip onboarding entirely.

## Key design decisions

- **This PC first**: prompts stay here until you connect a rented GPU over SSH. There is no account and no telemetry. A rental is a choice, and the chat says when prompts leave.
- **Ollama**: the app talks to Ollama. There is no llama.cpp backend.
- **Native Windows**: WinUI 3 with Fluent Design, not Electron
- **Markdown rendering**: rich text, code blocks, and syntax highlighting in responses
- **Governed extensibility**: plugins and tools operate within declared permission boundaries
- **Auditable by default**: every policy decision, network request, and plugin action is logged
