# InControl

## What this is

InControl is a WinUI 3 chat app for Ollama on Windows. The app version is 2.0.0.

Prompts stay on this PC until a rented GPU is connected over SSH. There is no llama.cpp backend.

The package identity is `InControl.App` at `2.0.0.0`, publisher `CN=5305D976-6952-4F00-9C21-3A5DB090359F`. There is no MSIX file in the repo. The product name stays InControl.

## Architecture

- WinUI 3 desktop UI
- Ollama HTTP API on this PC, or through an SSH local forward you start
- Chat history on this PC

## Key notes

- Windows only
- Build from source. See `docs/INSTALLATION.md`
- Tag `v0.3.0` is an older source-only release, not the app version in this tree
