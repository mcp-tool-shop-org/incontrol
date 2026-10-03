---
title: Getting Started
description: Install InControl from source and run the tests.
sidebar:
  order: 1
---

InControl is Ollama chat for Windows. It runs on this PC until you connect a GPU you rented. That connection is SSH, and the chat tells you when prompts leave this PC. See the repository's `docs/COMPUTE.md`.

## Prerequisites

- .NET 9.0 Runtime
- Windows 10 1809+ (Windows 11 recommended)
- A local LLM backend — we recommend [Ollama](https://ollama.ai/)

## Set up Ollama

```bash
# Install from https://ollama.ai/download
# Pull a model
ollama pull llama3.2

# Start the server
ollama serve
```

## Install InControl

App version 2.0.0 has no MSIX file in this repo. Package identity mcp-tool-shop.InControl-Desktop 2.0.0.0 is being prepared. Build the current tree from source. A fresh clone of current main is app 2.0.0, not tag v0.3.0. Tag v0.3.0 is an older source-only release. The window project compiles its assembly, then Windows App SDK packaging fails on a plain .NET SDK, so `dotnet test` is the check this release actually runs.

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet test tests/InControl.Core.Tests
```

Ollama listens on `http://127.0.0.1:11434` on this PC. A rented GPU is optional and lives in Settings, under **Where this chat runs**.

## Run tests

```bash
dotnet test tests/InControl.Core.Tests
```

## Verify build environment

```powershell
./scripts/verify.ps1
```
