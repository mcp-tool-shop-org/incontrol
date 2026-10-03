# Privacy & Security

**InControl — data handling details**

The privacy policy is [PRIVACY.md](../PRIVACY.md) at the root of this repository. The Microsoft Store listing links to that one. This page gives the technical detail behind it.

---

## Data Storage

Sessions, projects, remembered notes, settings and logs stay on this PC. InControl does not run an account service and it does not send telemetry.

The chat stays on this PC until you connect a rented GPU in **Where this chat runs**. That connection sends the conversation, the project's instructions, any remembered notes that match the message, and any attached files to Ollama on the machine you named. Disconnect, and the chat is local again. This is a different decision from the tool-URL allowlist.

### Storage Locations

| Data Type | Location | Purpose |
|-----------|----------|---------|
| Sessions | `%LOCALAPPDATA%\InControl\sessions\` | Conversation history |
| Projects and notes | `%LOCALAPPDATA%\InControl\` (`projects.json`, `memories.json`) | Projects, their instructions, and remembered notes |
| Logs | `%LOCALAPPDATA%\InControl\logs\` | Application logs for troubleshooting |
| Cache | `%LOCALAPPDATA%\InControl\cache\` | The Kokoro voice model, once downloaded |
| Config | `%LOCALAPPDATA%\InControl\config\` | User preferences and settings |
| Support | `%LOCALAPPDATA%\InControl\support\` | Support bundles you export |
| Exports | `%USERPROFILE%\Documents\InControl\exports\` | Data you export |

A Microsoft Store install may keep the `%LOCALAPPDATA%\InControl\` folder inside the app's own storage, `%LOCALAPPDATA%\Packages\mcp-tool-shop.InControl-Desktop_yn6b8xqrexa5j\`. Uninstalling the app removes that storage.

Session, project and note files are written to a temporary file and then swapped in, so a crash during a save leaves the previous file whole.

### What Is NOT Stored

- No data is stored on an InControl server. There is no InControl server.
- No telemetry is sent to Anthropic, Microsoft, or any third party.
- No usage analytics are collected.

---

## Network Activity

### Ollama, and a rental when you connect one

The app talks to Ollama. Chat stays on this PC until a rented GPU is connected. A rental is an SSH local forward, and the banner names that machine while it is connected.

### Other connections, each started by something you do

| Connection | When | What is sent |
|------------|------|--------------|
| Ollama model pull | You start a pull | The model name, to Ollama's registry through the Ollama the banner names |
| RunPod API | You press **Look up my RunPod pods** with `RUNPOD_API_KEY` set | The key, to list pods. The key is not stored, and the chat is not sent |
| GitHub | The first time voice is needed: a reply is spoken, or you open Settings | A download request for the Kokoro voice model (about 300 MB). Speech runs on this PC |
| ollama.com | You run diagnostics on the Help page | A reachability check |
| DuckDuckGo and public web pages | Web search is on and the model searches or reads a page | The model's search query, or a request for the page. Private and local addresses are refused. Each search is listed on the reply |

Offline is off when the app is installed. Turning it on refuses a rented GPU, a RunPod lookup, a model pull, and web search. Chat on this PC still works. It does not stop the voice model download, and it is not a kill switch for every socket.

There is no llama.cpp backend.

### What InControl Does NOT Do

- Does not check for updates. A Store install is updated by the Microsoft Store.
- Does not send crash reports.
- Does not send the chat to a rental unless you connect one in **Where this chat runs**.

---

## Secrets Policy

- The RunPod API key is read from the environment and never written to disk or to logs.
- The SSH private key stays where you keep it. InControl passes its path to the Windows OpenSSH client and does not copy or upload it.
- Message content is not written to the logs.

### Support Bundles

When you export a support bundle:
- **Included**: version and runtime info, log files, sanitized configuration
- **Excluded**: session content, API keys, tokens, credentials
- **Opt-in only**: session metadata

---

## Data Retention

- **Logs**: rolling files, 10 MB each, five files kept.
- **Sessions, projects and notes**: kept until you delete them.
- **Clear All Memory** (Settings) deletes every remembered note. It does not delete chats.
- **Export** copies a session as JSON to the clipboard.
- **Delete** removes a session file. If the file cannot be deleted, the session stays in the list and the window says so.

---

## Write Boundaries

InControl writes only to:

- `%LOCALAPPDATA%\InControl\*` (or the Store app's own storage)
- `%USERPROFILE%\Documents\InControl\exports\*`

It does not write to system directories, Program Files, its own install folder, or other users' directories.

---

## Dependency Security

- CI runs `dotnet list package --vulnerable --include-transitive`.
- Dependabot opens grouped update pull requests.

| Package | Purpose | Notes |
|---------|---------|-------|
| Microsoft.WindowsAppSDK | UI framework | Microsoft-maintained, bundled in the package |
| OllamaSharp | Ollama HTTP client | Talks to loopback only |
| KokoroSharp | On-device speech | Its model is downloaded data, not code |

---

## Threat Model

### In Scope

- Local data confidentiality
- Keeping the chat on this PC until you connect a rental, and saying where it goes when you do
- Not exposing Ollama: a rental is reached through an SSH forward to its loopback port, never a public port

### Out of Scope

- Physical access attacks
- A compromised operating system
- A malicious inference backend, or a rental host you do not trust

---

## Reporting Security Issues

Follow [SECURITY.md](../SECURITY.md). Do not open a public issue for a vulnerability.

---

*Last updated: 2026-10-02*
