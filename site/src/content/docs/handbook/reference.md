---
title: Reference
description: NuGet packages, plugin SDK, error codes, and security scope.
sidebar:
  order: 4
---

## NuGet packages

| Package | Description |
|---------|-------------|
| InControl.Core | Domain models, conversation types, assistant profiles, plugin manifest types, policy types, and shared abstractions |
| InControl.Inference | LLM backend abstraction layer with streaming chat, model management, and health checks. Includes Ollama implementation |

```bash
dotnet add package InControl.Core
dotnet add package InControl.Inference
```

## Streaming chat example

```csharp
var client = inferenceClientFactory.Create("ollama");
await foreach (var token in client.StreamChatAsync(messages))
{
    Console.Write(token);
}
```

## Assistant profiles

InControl ships three built-in assistant profiles. Profiles are immutable records that control tone, verbosity, explanation level, and risk tolerance.

| Profile | Tone | Verbosity | Explanation | Risk Tolerance |
|---------|------|-----------|-------------|----------------|
| Default | Professional | Concise | On request | Moderate |
| Minimal | Professional | Brief | Minimal | Low |
| Detailed | Professional | Detailed | Proactive | High |

## Plugin SDK

Plugins extend InControl through a manifest-validated, sandboxed system. To create a plugin:

1. Define a `PluginManifest` with your plugin's ID, version, permissions, and capabilities.
2. Extend `PluginBase` and override `OnExecuteAsync` to handle actions.
3. Package the plugin with its manifest.

Plugins declare permissions for file access, memory, network, UI, conversation context, and settings. The manifest validator enforces consistency between declared risk levels and requested permissions.

Risk levels determine what a plugin can do:

| Level | Name | Allowed operations |
|-------|------|-------------------|
| 1 | ReadOnly | Read-only operations, no side effects |
| 2 | LocalMutation | Can modify local data (files, memory) |
| 3 | Network | Can access network through ConnectivityManager |
| 4 | SystemAdjacent | Reserved, not available |

## Error codes

InControl uses structured error codes organized by category:

| Range | Category | Examples |
|-------|----------|---------|
| 0-99 | General | Unknown, InvalidArgument, Timeout, Cancelled |
| 100-199 | Connection | ConnectionFailed, ConnectionTimeout, HostNotFound |
| 200-299 | Inference | ModelNotFound, ContextExceeded, StreamInterrupted |
| 300-399 | Storage | FileNotFound, PermissionDenied, CorruptedData |
| 400-499 | Configuration | ConfigurationInvalid, ConfigurationMissing |
| 500-599 | Validation | ValidationFailed, RequiredFieldMissing |
| 600-699 | Assistant Tools | ToolExecutionFailed, ToolPermissionDenied |

Every error includes a code, message, and severity (Low, Medium, High, Critical) with an optional hint for resolution.

## Health checks

Three built-in health checks report system readiness:

| Check | Category | What it verifies |
|-------|----------|-----------------|
| AppHealthCheck | Application | Application state and configuration |
| InferenceHealthCheck | Inference | Backend connection, model availability |
| StorageHealthCheck | Storage | File system access, disk space |

## Security and data scope

| Aspect | Detail |
|--------|--------|
| Data accessed | Local Ollama API (localhost), chat history in local storage, model configuration files |
| Data NOT accessed | No cloud sync, no telemetry, no analytics |
| Permissions | Localhost network (Ollama API), file system for chat history. MSIX sandboxed |
| Support bundles | Never include conversation content or secrets. Sanitized configs only. |

See [SECURITY.md](https://github.com/mcp-tool-shop-org/InControl-Desktop/blob/main/SECURITY.md) for vulnerability reporting.
