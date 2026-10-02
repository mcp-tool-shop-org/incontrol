# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.3.x   | :white_check_mark: Current |

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

InControl Desktop is a **local-first** WinUI 3 desktop application for private LLM chat.

- **Data accessed:** Local Ollama API (localhost), chat history in local storage, model configuration files, optional extensions/plugins
- **Data NOT accessed:** No cloud sync. No telemetry. No analytics. All inference runs locally via Ollama
- **Permissions:** Localhost network (Ollama API), file system for chat history and configuration. MSIX sandboxed. No elevated permissions required
- **No telemetry** is collected or sent

### Out of Scope

- Vulnerabilities in Ollama itself (report to Ollama project)
- Social engineering or physical access attacks
