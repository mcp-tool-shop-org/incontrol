---
title: Beginner's Guide
description: A step-by-step introduction to InControl for new users.
sidebar:
  order: 99
---

This guide walks you through everything you need to start using InControl, from installation to your first conversation.

## What is InControl?

InControl is an Ollama chat application for Windows. The Microsoft Store lists it as InControl-Desktop. Chat stays on this PC until a rented GPU is connected. Nothing is sent to a rental until you connect one. A rental sends the prompts you submit to Ollama on that machine, and the banner names that machine while those prompts leave.

## Prerequisites

Before installing InControl, make sure you have:

- **Windows 10 version 2004 or later, x64** (Windows 11 recommended)
- **A GPU Ollama can use** -- for example an RTX 3060 (8 GB VRAM) at minimum, an RTX 4080/5080 (16 GB VRAM) recommended. Without one, Ollama runs on the CPU, slowly. A rented GPU is the other option.
- **Ollama** -- the local LLM backend. Install from [ollama.com/download](https://ollama.com/download)

## Installation

### Microsoft Store (recommended)

Open [InControl-Desktop](https://apps.microsoft.com/detail/9N1FG39JWF83) in the Microsoft Store and choose **Get**. The package includes everything it needs, including .NET, and the Store keeps it updated.

### Build from source

You need the .NET 9 SDK. Clone the repository, run the tests, and start the app:

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet test tests/InControl.Core.Tests
dotnet run --project src/InControl.App
```

## First-run walkthrough

When you launch InControl for the first time, the onboarding wizard guides you through four steps:

1. **Welcome** -- introduces InControl. Chat stays on this PC until a rented GPU is connected.
2. **Backend Check** -- InControl looks for a running Ollama on `127.0.0.1:11434`. If Ollama is not running, start it with `ollama serve` in a separate terminal.
3. **Model Selection** -- pick a model from those available on your Ollama instance. If you have no models yet, pull one first: `ollama pull llama3.2`.
4. **Ready** -- you are all set. Click "Start" to begin your first conversation.

You can skip onboarding if you prefer to configure things manually.

## Core concepts

### Conversations and sessions

Each conversation is stored as a session file in `%LOCALAPPDATA%\InControl\sessions\`. Nothing is sent to a rental until you connect one. A rental sends the prompts you submit to Ollama on that machine. The banner names that machine while those prompts leave. Right-click a session to rename, duplicate, pin, export it as JSON, or delete it.

### Projects and notes

Sessions live in projects. **General** is where a session goes by default. A project can carry instructions that are added to every message in it. Right-click a session to remember a note for that session or its project. Notes that match a new message are sent along with it, so they reach a rental too while one is connected. **Clear All Memory** in Settings deletes every note and keeps the chats.

### Voice

InControl can read replies aloud with Kokoro, which runs on this PC. The first time it speaks, it downloads the voice model, about 300 MB. Turn off auto-speak in Settings if you don't want replies read aloud.

### The offline switch

Offline is off when the app is installed. Turning it on refuses a rented GPU, a RunPod lookup, and a model download. Chat on this PC still works. The switch does not stop the voice model download. It is not a kill switch for every socket, and it is not the default.

Tool-URL allowlisting is a different control. See the repository file `docs/CONNECTIVITY.md`.

### Assistant profiles

InControl lets you adjust how the assistant communicates:

- **Default** -- professional tone, concise answers, explains when asked.
- **Minimal** -- brief answers, minimal explanation, fewer warnings. Good for advanced users.
- **Detailed** -- longer answers, proactive explanation, more cautious. Good for learning.

### Plugins

InControl has a plugin system: each plugin declares the permissions it needs in a manifest, and runs sandboxed to what it declares. Version 2.0 does not install plugins from the app. The SDK is described in the [Reference](/incontrol/handbook/reference/).

## Key settings

The most important settings to know about:

| Setting | Where | What it controls |
|---------|-------|-----------------|
| Theme | App settings | Light, Dark, or System theme |
| Default Model | Inference settings | Which model loads by default |
| Temperature | Inference settings | Creativity of responses (lower = more focused, higher = more varied) |
| Context Size | Ollama settings | How much conversation history the model sees (in tokens) |
| GPU Layers | Ollama settings | How much of the model runs on GPU vs CPU (-1 = all on GPU) |

See the [Configuration](/incontrol/handbook/configuration/) page for the full list.

## Troubleshooting for beginners

**"No models available" after launch**

Ollama needs to be running and have at least one model pulled:

```bash
ollama serve          # start the server
ollama pull llama3.2  # download a model
```

Then restart InControl or re-run the backend check.

**App launches but shows a blank screen**

A Store install needs nothing else. For a source build, check that the .NET 9 SDK is installed with `dotnet --list-sdks`.

**Responses are very slow**

- Check that your GPU is being used: `ollama ps` shows how much of the model is on the GPU.
- Try a smaller model (e.g., `llama3.2` instead of a 70B variant).
- In Ollama settings, make sure GPU Layers is set to `-1` (offload everything to GPU).

**"Connection refused" errors**

Ollama defaults to `http://127.0.0.1:11434`. Confirm it is running with:

```bash
curl http://127.0.0.1:11434/api/tags
```

If you changed the Ollama port, update the `BaseUrl` in Inference:Ollama settings to match.

**How do I report a bug?**

1. Open InControl and use the "Copy Diagnostics" feature to create a support bundle. This includes logs and system info but never your conversation content.
2. Open an [issue on GitHub](https://github.com/mcp-tool-shop-org/incontrol/issues) and attach the support bundle.
