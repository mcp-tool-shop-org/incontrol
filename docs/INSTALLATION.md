# Installation

Version 2.0.0 is a source build of the current tree. There is no MSIX in this repository, and there is no installer to run from the repo. A later package identity `InControl.App` `2.0.0.0` is being prepared. The publisher is still unknown.

The repository is [mcp-tool-shop-org/incontrol](https://github.com/mcp-tool-shop-org/incontrol). A fresh clone of current main is app 2.0.0, not tag `v0.3.0`. Tag `v0.3.0` is an older source-only release. That tag has no MSIX. A GitHub release does not attach a package.

## What you need

| | |
|---|---|
| OS | Windows 10 1809+ or Windows 11, x64 |
| .NET | SDK 9 |
| Models | [Ollama](https://ollama.com/download) on this PC, listening on `http://127.0.0.1:11434` |

A rented GPU is optional. It is an SSH local forward, not an installer option.

## Build

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build
dotnet run --project src/InControl.App
```

Pull a model with Ollama (`ollama pull llama3.2`) before you expect a reply.

`dotnet build` of the app compiles the assembly. Windows App SDK packaging can then fail on a machine that does not have the Appx MSBuild task. That failure is the missing MSIX step. The app project still builds its DLL. There is no signed package to install, sideload, or verify.

## Data on this PC

Settings and logs for a local run live under `%LOCALAPPDATA%\InControl\`. Removing the clone does not delete that folder. Delete it yourself if you want the local history gone.

## Updating

Check out a newer commit and build again. Do not look for `InControl-Desktop-x.y.z.msix`. That name belonged to a dormant prototype and it is not how this repository ships.
