---
title: Getting Started
description: Install InControl from the Microsoft Store or from source.
sidebar:
  order: 1
---

InControl is Ollama chat for Windows. It runs on this PC until you connect a GPU you rented. That connection is SSH, and the chat tells you when prompts leave this PC. See [Where the chat runs](/incontrol/handbook/where-it-runs/).

## Prerequisites

- Windows 10 version 2004 or later, x64 (Windows 11 recommended)
- [Ollama](https://ollama.com/download) on this PC

## Set up Ollama

```bash
# Install from https://ollama.com/download, then pull a model
ollama pull llama3.2
```

Ollama listens on `http://127.0.0.1:11434`. The Ollama desktop app starts it for you. Without the app, run `ollama serve`.

## Install from the Microsoft Store

Open [InControl-Desktop](https://apps.microsoft.com/detail/9N1FG39JWF83) in the Microsoft Store, or search the Store for InControl-Desktop. The Start menu tile says InControl.

The Store package carries its own .NET and Windows App SDK runtimes, so there is nothing else to install. The Store keeps it up to date.

## Build from source

You need the .NET 9 SDK.

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet test tests/InControl.Core.Tests
dotnet run --project src/InControl.App
```

`dotnet run` starts an unpackaged Debug build. A plain .NET SDK cannot produce the MSIX, because the Windows App SDK packaging step needs Visual Studio's MSBuild. The repository's `packaging/README.md` has the exact command.

## Run the tests

```bash
dotnet test tests/InControl.Core.Tests
dotnet test tests/InControl.Services.Tests
dotnet test tests/InControl.Inference.Tests
```

## Verify the build environment

```powershell
./scripts/verify.ps1
```

## First chat

1. Start InControl. The bar across the top says the chat runs on this PC.
2. Pick a model in the composer. Models come from the Ollama on this PC.
3. Type a message. The reply streams into the thread.

A rented GPU is optional. It lives in Settings, under **Where this chat runs**.
