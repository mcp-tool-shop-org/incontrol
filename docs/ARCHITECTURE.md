# InControl Architecture

## Overview

InControl is a Windows desktop application for Ollama chat, built with WinUI 3. The default target is Ollama on this PC. A rented GPU is an SSH local forward to Ollama bound on that machine's loopback. The HTTP client does not learn a second protocol. See [COMPUTE.md](./COMPUTE.md).

## Design Principles

1. **Separation of Concerns** - Each layer has a single responsibility
2. **Dependency Inversion** - Depend on abstractions, not implementations
3. **Testability** - Core logic is unit-testable without UI
4. **Extensibility** - New LLM backends can be added without changing upper layers
5. **Execution Boundaries** - All side effects go through controlled interfaces
6. **Deterministic State** - Immutable models with explicit transitions

## Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                        InControl.App                             │
│  WinUI 3 Application Host                                   │
│  - App.xaml / MainWindow                                    │
│  - DI Container Setup                                       │
│  - Navigation                                               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     InControl.ViewModels                         │
│  MVVM Presentation Layer                                    │
│  - ChatViewModel                                            │
│  - SettingsViewModel                                        │
│  - Commands and State                                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      InControl.Services                          │
│  Business Logic & Orchestration                             │
│  - IChatService / ChatService                               │
│  - IFileStore / FileStore (path boundaries)                 │
│  - IHealthService / HealthService                           │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     InControl.Inference                          │
│  LLM Backend Abstraction                                    │
│  - IInferenceClient (abstraction)                           │
│  - FakeInferenceClient (testing)                            │
│  - OllamaInferenceClient (future)                           │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        InControl.Core                            │
│  Shared Types & Contracts                                   │
│  - Message, Conversation, AppState (immutable)              │
│  - InControlError, Result<T> (error handling)                    │
│  - BuildInfo, TrustReport (trust signals)                   │
│  - StateSerializer (deterministic JSON)                     │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```
InControl/
├── src/
│   ├── InControl.App/                 # WinUI 3 Application
│   │   ├── App.xaml(.cs)
│   │   ├── MainWindow.xaml(.cs)
│   │   ├── Views/
│   │   ├── Controls/
│   │   └── InControl.App.csproj
│   │
│   ├── InControl.ViewModels/          # MVVM ViewModels
│   │   ├── ChatViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   └── InControl.ViewModels.csproj
│   │
│   ├── InControl.Services/            # Business Logic
│   │   ├── Interfaces/
│   │   ├── Storage/              # IFileStore, FileStore
│   │   ├── Health/               # IHealthCheck, HealthService
│   │   └── InControl.Services.csproj
│   │
│   ├── InControl.Inference/           # LLM Backends
│   │   ├── Interfaces/
│   │   ├── Fakes/                # FakeInferenceClient
│   │   └── InControl.Inference.csproj
│   │
│   └── InControl.Core/                # Shared Types
│       ├── Models/               # Message, Conversation, etc.
│       ├── Errors/               # InControlError, Result<T>
│       ├── State/                # AppState, StateSerializer
│       ├── Trust/                # BuildInfo, TrustReport
│       └── InControl.Core.csproj
│
├── tests/
│   ├── InControl.Core.Tests/          # 1,343 tests
│   ├── InControl.Services.Tests/      # 68 tests
│   └── InControl.Inference.Tests/     # 19 tests
│
├── scripts/
│   ├── verify.ps1                # CI-like verification (Windows)
│   ├── verify.sh                 # CI-like verification (Unix)
│   ├── coverage.ps1              # Coverage collection (Windows)
│   └── coverage.sh               # Coverage collection (Unix)
│
├── docs/
│   ├── ARCHITECTURE.md           # This file
│   └── PHASE2_ACCEPTANCE.md      # Phase 2 gate status
│
├── Directory.Build.props         # Shared MSBuild properties
├── Directory.Packages.props      # Central package management
├── InControl.sln
├── LICENSE                       # MIT License
└── README.md
```

## Dependency Flow

```
InControl.App ──────► InControl.ViewModels
    │                 │
    │                 ▼
    │           InControl.Services
    │                 │
    │                 ▼
    └──────────► InControl.Inference
                      │
                      ▼
                 InControl.Core ◄────── (all projects reference)
```

## Key Abstractions

### IInferenceClient

The core abstraction for LLM communication:

```csharp
public interface IInferenceClient
{
    string BackendName { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default);
    Task<ModelInfo?> GetModelAsync(string modelId, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, CancellationToken ct = default);
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);
}
```

### IFileStore

Filesystem abstraction with path boundary enforcement:

```csharp
public interface IFileStore
{
    string AppDataPath { get; }
    bool IsPathAllowed(string path);
    Task<Result<string>> ReadTextAsync(string relativePath, CancellationToken ct = default);
    Task<Result> WriteTextAsync(string relativePath, string content, CancellationToken ct = default);
    Task<Result> DeleteAsync(string relativePath, CancellationToken ct = default);
    Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);
}
```

### Result<T>

Monadic error handling pattern:

```csharp
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public T? Value { get; }
    public InControlError? Error { get; }

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper);
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder);
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<InControlError, TResult> onFailure);
}
```

### IHealthCheck

Health monitoring probe interface:

```csharp
public interface IHealthCheck
{
    string Name { get; }
    string Category { get; }
    Task<HealthProbeResult> CheckAsync(CancellationToken ct = default);
}
```

## Error Handling Strategy

Phase 2 introduced structured error handling:

1. **InControlError** - Immutable error record with code, message, suggestions
2. **Result<T>** - Either success value or error (no exceptions for expected failures)
3. **ErrorCode** - 30+ categorized error codes
4. **ErrorSeverity** - Info, Warning, Error, Critical

Error flow:
- InControl.Inference returns `Result<T>` from operations
- InControl.Services uses `Map`/`Bind` for composition
- InControl.ViewModels uses `Match` for UI presentation
- Exceptions reserved for truly exceptional conditions

## State Management

Phase 2 introduced deterministic state:

1. **Immutable Records** - `Message`, `Conversation`, `AppState` are sealed records
2. **Factory Methods** - `With*` methods return new instances
3. **StateSerializer** - Deterministic JSON serialization
4. **Round-trip Tests** - All state types have serialization tests

```csharp
// Immutable state transitions
var newState = state
    .WithConversation(conversation)
    .WithActiveConversation(conversation.Id)
    .WithModelSelection(ModelSelectionState.WithModel("llama3.2"));
```

## Trust & Security

Phase 2 added trust infrastructure:

1. **BuildInfo** - Version, commit hash, build timestamp
2. **TrustReport** - Aggregated security and runtime info
3. **SecurityConfig** - Path boundaries, inference isolation
4. **TrustLevel** - Computed trust level (High/Medium/Low)

## Configuration

Configuration is loaded from multiple sources with precedence:

1. `appsettings.json` - Base configuration
2. `appsettings.Development.json` - Dev overrides
3. Environment variables - Runtime overrides
4. User settings file - User preferences

## Threading Model

- UI runs on the main thread (WinUI 3 STA)
- Inference operations run on background threads
- ViewModels marshal results back to UI thread via `DispatcherQueue`
- All async methods support `CancellationToken`

---

## Phase 2 Status

All Phase 2 gates have been implemented:

| Gate | Status |
|------|--------|
| Build & Test Integrity | ✅ |
| Test Coverage Floor | ✅ |
| Execution Boundary Enforcement | ✅ |
| Deterministic State & Persistence | ✅ |
| Health, Errors, and Failure Clarity | ✅ |
| Architecture Lock | ⏳ Awaiting sign-off |
| User-Visible Trust Signals | ✅ |

See `docs/PHASE2_ACCEPTANCE.md` for detailed gate status.

---

## Out-of-Scope for Phase 2

The following are explicitly not part of Phase 2:

- Multi-user support
- Cloud sync or backup
- Plugin/extension system
- Custom model training or fine-tuning
- Voice input/output
- Cross-platform support (Windows only)
- Web interface
- Mobile companion app
- Ollama installation/management (user responsibility)

## Extension Points

These interfaces allow future extension without architectural changes:

| Interface | Purpose | Example Extensions |
|-----------|---------|-------------------|
| `IInferenceClient` | Add new LLM backends | llama.cpp, vLLM, LocalAI |
| `IFileStore` | Add new storage backends | Cloud storage, encrypted storage |
| `IHealthCheck` | Add new health probes | GPU monitoring, disk space |
| Configuration | Add new settings sections | Theming, keyboard shortcuts |

## Non-Goals

These are explicitly not goals for InControl:

- Cross-platform support (Windows App SDK is Windows-only)
- Web interface (desktop-first design)
- Mobile companion app
- Model fine-tuning or training
- Multi-user or team features
- Cloud deployment

---

## Phase 9: Policy, Governance & Enterprise Controls

Phase 9 introduced a comprehensive policy layer for enterprise governance:

### Policy Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Policy Sources                         │
│  Organization > Team > User > Session > Default            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      PolicyEngine                           │
│  - Load/merge policies by precedence                       │
│  - EvaluateTool/Plugin/Memory/Connectivity/Update          │
│  - Deterministic decisions with explanations               │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ PolicyGoverned  │ │ PolicyGoverned  │ │ PolicyGoverned  │
│ ToolRegistry    │ │ PluginHost      │ │ MemoryManager   │
└─────────────────┘ └─────────────────┘ └─────────────────┘
              │               │               │
              ▼               ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ PolicyGoverned  │ │ PolicyDiagnostics│ │ PolicyViewer    │
│ Connectivity    │ │ (Tracing/Debug)  │ │ (Admin UX)      │
│ Manager         │ └─────────────────┘ └─────────────────┘
└─────────────────┘
```

### Key Policy Types

| Type | Purpose |
|------|---------|
| `PolicyDocument` | JSON schema for policy files |
| `PolicyEngine` | Loads, merges, and evaluates policies |
| `PolicyEvaluationResult` | Decision with source, reason, constraints |
| `PolicyGovernedToolRegistry` | Wraps ToolRegistry with policy checks |
| `PolicyGovernedPluginHost` | Wraps PluginHost with approval flow |
| `PolicyGovernedMemoryManager` | Enforces retention and capacity limits |
| `PolicyGovernedConnectivityManager` | Mode and domain restrictions |
| `PolicyGovernedUpdateManager` | Channel and deferral enforcement |
| `PolicyDiagnostics` | Tracing, explanation, and validation |
| `PolicyViewer` | Human-readable views for admin UX |

### Policy Decision Types

```csharp
public enum PolicyDecision
{
    Allow,              // Permitted without restrictions
    Deny,               // Blocked by policy
    AllowWithApproval,  // Requires operator approval
    AllowWithConstraints // Permitted with constraints (e.g., rate limits)
}
```

### Policy File Locations

```
Organization: %ProgramData%/InControl/policy.json    (IT-deployed)
Team:         %APPDATA%/InControl/team-policy.json   (Team settings)
User:         %LOCALAPPDATA%/InControl/policy.json   (User prefs)
Session:      In-memory only (programmatic)
```

### Policy Precedence

1. **Organization** (highest) - IT-deployed, locked rules cannot be overridden
2. **Team** - Shared team settings
3. **User** - Personal preferences
4. **Session** - Runtime approvals
5. **Default** (lowest) - Built-in defaults (allow most)

### Policy Enforcement Points

| Area | Enforcement |
|------|-------------|
| **Tools** | Allow/Deny/RequireApproval lists, wildcards, constraints |
| **Plugins** | Risk level limits, trusted authors, approval workflow |
| **Memory** | Enabled, max entries, retention days, excluded categories |
| **Connectivity** | Allowed modes, blocked/allowed domains, telemetry |
| **Updates** | Auto-update, required channel, deferral days, minimum version |

### Admin UX

```csharp
var viewer = new PolicyViewer(engine);

// Quick status
var status = viewer.GetStatusView();
// { IsHealthy: true, ActiveSourceCount: 2, TotalRuleCount: 15 }

// Explain tool decision
var toolView = viewer.GetToolView("shell-execute");
// { Decision: Deny, Source: Organization, EvaluationPath: "Organization.deny(shell-*)=Deny" }

// Export full report
var report = viewer.ExportAsText();
```

---

## Phase 3 Preview

Phase 3 will focus on:

1. Service implementations (ChatService, SettingsService)
2. Ollama integration (OllamaInferenceClient)
3. UI implementation (XAML views and bindings)
4. End-to-end conversation flow

Architecture freeze applies: no new projects without plan update.
