# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 2.0.1 (this tree) | :white_check_mark: Current. Microsoft Store package InControl-Desktop, or a source build. A full-trust desktop app, not AppContainer-sandboxed. Does not ask for elevation. |
| 0.3.x (tag v0.3.0) | Older published source-only GitHub release. That tag has no MSIX. Not the app version in this tree. |

## Reporting a Vulnerability

**Email:** 64996768+mcp-tool-shop@users.noreply.github.com

1. **Do NOT** open a public issue for security vulnerabilities
2. Email the address above with a detailed description
3. Include steps to reproduce if applicable

### Response timeline

| Action | Target |
|--------|--------|
| Acknowledge report | 48 hours |
| Assess severity | 7 days |
| Release fix | 30 days |

## Scope

InControl is a WinUI 3 chat app. Ollama on this PC is the default. A GPU rental is an SSH local forward you start yourself.

- **Data accessed:** Ollama on this PC, or Ollama on a machine you connect over SSH. Chat history and settings in local storage.
- **Data NOT accessed:** No InControl account, no cloud sync, and no telemetry. Inference stays on this PC until you connect a rental. Then the prompts you send go to that machine.
- **Permissions:** Loopback HTTP, an SSH client when you connect a rental, and the file system for chat history. This tree's build (app 2.0.0) is not an MSIX and is not sandboxed by AppContainer. It does not ask for elevation. A later package identity `mcp-tool-shop.InControl-Desktop` `2.0.0.0` is being prepared. There is no MSIX file in the repo. Tag v0.3.0 is an older source-only release.
- **No telemetry** is collected or sent

### Out of Scope

- Vulnerabilities in Ollama itself (report to Ollama project)
- Social engineering or physical access attacks
