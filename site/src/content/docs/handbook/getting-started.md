---
title: Getting Started
description: Install and run InControl Desktop.
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

### From Release (recommended)

1. Download the latest MSIX package from [Releases](https://github.com/mcp-tool-shop-org/InControl-Desktop/releases)
2. Double-click to install
3. Launch from Start Menu

### From Source

```bash
git clone https://github.com/mcp-tool-shop-org/InControl-Desktop.git
cd InControl-Desktop
dotnet restore
dotnet build

# Run (requires Ollama running locally)
dotnet run --project src/InControl.App
```

## Run tests

```bash
dotnet test
```

## Verify build environment

```powershell
./scripts/verify.ps1
```
