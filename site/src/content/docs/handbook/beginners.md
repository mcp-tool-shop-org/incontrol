---
title: Beginner's Guide
description: A step-by-step introduction to InControl Desktop for new users.
sidebar:
  order: 99
---

This guide walks you through everything you need to start using InControl Desktop, from installation to your first conversation.

## What is InControl Desktop?

InControl Desktop is a local AI chat application for Windows. It connects to a local LLM backend (such as Ollama) running on your machine and provides a native desktop interface for conversing with large language models. No data leaves your computer -- all inference runs locally on your GPU.

InControl is built for users who want privacy, control, and GPU-accelerated performance without relying on cloud services.

## Prerequisites

Before installing InControl, make sure you have:

- **Windows 10 version 1809 or later** (Windows 11 recommended)
- **.NET 9.0 Runtime** -- download from [dot.net](https://dotnet.microsoft.com/download/dotnet/9.0) if not already installed
- **An NVIDIA GPU** -- minimum RTX 3060 (8 GB VRAM), recommended RTX 4080/5080 (16 GB VRAM)
- **Ollama** -- the recommended local LLM backend. Install from [ollama.ai/download](https://ollama.ai/download)

To verify .NET is installed, open a terminal and run:

```bash
dotnet --list-runtimes
```

You should see a line containing `Microsoft.NETCore.App 9.0` or later.

## Installation

### Option A: MSIX installer (recommended)

1. Go to the [Releases page](https://github.com/mcp-tool-shop-org/InControl-Desktop/releases).
2. Download the latest `.msix` file.
3. Double-click the file to install.
4. Launch InControl from the Start Menu.

### Option B: Build from source

```bash
git clone https://github.com/mcp-tool-shop-org/InControl-Desktop.git
cd InControl-Desktop
dotnet restore
dotnet build
dotnet run --project src/InControl.App
```

## First-run walkthrough

When you launch InControl for the first time, the onboarding wizard guides you through four steps:

1. **Welcome** -- introduces InControl and confirms that everything runs locally.
2. **Backend Check** -- InControl looks for a running Ollama instance on `localhost:11434`. If Ollama is not running, start it with `ollama serve` in a separate terminal.
3. **Model Selection** -- pick a model from those available on your Ollama instance. If you have no models yet, pull one first: `ollama pull llama3.2`.
4. **Ready** -- you are all set. Click "Start" to begin your first conversation.

You can skip onboarding if you prefer to configure things manually.

## Core concepts

### Conversations and sessions

Each conversation is stored as a session file in `%LOCALAPPDATA%\InControl\sessions\`. Sessions are local JSON files -- they are never uploaded anywhere. You can export conversations from the app and delete them at any time.

### Connectivity modes

InControl has three network modes that you control:

| Mode | What it means |
|------|---------------|
| **Offline Only** | No network access at all. Everything stays local. This is the default. |
| **Assisted** | Network requests require per-endpoint approval before they execute. |
| **Connected** | Full network access, but every request is logged to the audit trail. |

You can switch modes at any time from the connectivity panel. If anything feels wrong, the "Go Offline" button cuts all network activity instantly.

### Assistant profiles

InControl lets you adjust how the assistant communicates:

- **Default** -- professional tone, concise answers, explains when asked.
- **Minimal** -- brief answers, minimal explanation, fewer warnings. Good for advanced users.
- **Detailed** -- longer answers, proactive explanation, more cautious. Good for learning.

### Plugins

InControl supports plugins that extend what the assistant can do. Each plugin declares the permissions it needs in a manifest file. You review and approve permissions before a plugin can run. Plugins are sandboxed so they can only access what they declare.

## Key settings

The most important settings to know about:

| Setting | Where | What it controls |
|---------|-------|-----------------|
| Theme | App settings | Light, Dark, or System theme |
| Default Model | Inference settings | Which model loads by default |
| Temperature | Inference settings | Creativity of responses (lower = more focused, higher = more varied) |
| Context Size | Ollama settings | How much conversation history the model sees (in tokens) |
| GPU Layers | Ollama settings | How much of the model runs on GPU vs CPU (-1 = all on GPU) |

See the [Configuration](/InControl-Desktop/handbook/configuration/) page for the full list.

## Troubleshooting for beginners

**"No models available" after launch**

Ollama needs to be running and have at least one model pulled:

```bash
ollama serve          # start the server
ollama pull llama3.2  # download a model
```

Then restart InControl or re-run the backend check.

**App launches but shows a blank screen**

Make sure .NET 9.0 Runtime is installed. Run `dotnet --list-runtimes` to check. If it is missing, install it from [dot.net](https://dotnet.microsoft.com/download/dotnet/9.0).

**Responses are very slow**

- Check that your GPU is being used: the Ollama logs will show CUDA loading on startup.
- Try a smaller model (e.g., `llama3.2` instead of a 70B variant).
- In Ollama settings, make sure GPU Layers is set to `-1` (offload everything to GPU).

**"Connection refused" errors**

Ollama defaults to `http://localhost:11434`. Confirm it is running with:

```bash
curl http://localhost:11434/api/tags
```

If you changed the Ollama port, update the `BaseUrl` in Inference:Ollama settings to match.

**How do I report a bug?**

1. Open InControl and use the "Copy Diagnostics" feature to create a support bundle. This includes logs and system info but never your conversation content.
2. Open an [issue on GitHub](https://github.com/mcp-tool-shop-org/InControl-Desktop/issues) and attach the support bundle.
