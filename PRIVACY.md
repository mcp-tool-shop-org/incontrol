# Privacy Policy for InControl

**Last Updated: October 2, 2026**

## Overview

InControl is an Ollama chat app for Windows, published in the Microsoft Store as InControl-Desktop by mcp-tool-shop. The chat runs on this PC until you connect a GPU you rented over SSH. That choice is shown on the chat, and it is separate from any tool-URL permission.

## Data Collection

**InControl does not run an account service and does not collect telemetry.** A chat on this PC stays on this PC. If you connect a GPU rental, what InControl sends to the model goes to Ollama on that machine through SSH.

### What stays on your device:
- Conversations and chat history, until you connect a rental
- Files you attach to a message, saved with that chat
- Projects, project instructions, and notes you ask InControl to remember
- Your settings and preferences
- The private key file you choose (it is not uploaded)
- Logs and the voice model cache

### What we do NOT collect:
- Personal information
- Usage analytics
- Conversation content
- Telemetry data
- Location data
- Device identifiers

## What a Prompt Contains

Each message to the model carries the conversation so far. It can also carry the project's instructions, a few of your remembered notes that match the message, and any files you attach: the text of a text or code file, or the image itself. Attached files are saved with the chat on this PC. On this PC, all of that goes only to your local Ollama. While a rental is connected, all of that goes to Ollama on the rented machine. InControl does not get anything back from that machine except the replies.

## Network Connections

InControl does not run its own cloud, does not require an account, and does not sync a profile. Apart from Ollama on this PC, it connects to the network only for these things:

1. **A rental you connect.** Prompts go through an SSH port forward to the machine you chose.
2. **Pulling a model.** Ollama downloads it from its registry when you start a pull. The request goes through the Ollama the banner is naming.
3. **RunPod lookup.** If `RUNPOD_API_KEY` is in your environment, InControl can list pods that are already running. Lookup does not start a pod and does not store the key.
4. **The voice model.** The first time voice is needed, when a reply is spoken or when you open Settings, InControl downloads the Kokoro voice model (about 300 MB) from GitHub (`github.com/taylorchu/kokoro-onnx`) into the app's cache. Speech itself runs on this PC. With auto-speak off and Settings unopened, it is not downloaded.
5. **Diagnostics.** When you run diagnostics on the Help page, InControl checks that `https://ollama.com` is reachable.
6. **Web search, only when you turn it on.** The **Web** button next to the paperclip is off until you turn it on. While it is on, a model that can use tools may search the web. InControl sends the model's search query to DuckDuckGo (`html.duckduckgo.com`) and may fetch public pages the model asks to read. Pages on this PC or on a private network are refused. The reply lists every search and page. Offline mode turns web search off.

Links on the Model Manager page, such as the Ollama download page, open in your browser.

The offline switch turns off a rental, RunPod lookup, model pulls, and web search. It does not stop the voice model download, and it does not claim to block every socket. Chat on this PC still works while it is on.

InControl does not check for updates itself. When it is installed from the Microsoft Store, the Store handles updates.

## Data Storage

Application data is stored on this PC in `%LOCALAPPDATA%\InControl\`: conversations, projects, remembered notes, settings, logs, and the voice model cache. Models pulled through Ollama stay in Ollama's own storage.

When InControl is installed from the Microsoft Store, Windows may keep that folder inside the app's own storage, under `%LOCALAPPDATA%\Packages\mcp-tool-shop.InControl-Desktop_yn6b8xqrexa5j\`. Uninstalling the app from Windows Settings removes that storage. If the folder is at `%LOCALAPPDATA%\InControl\` instead, delete it yourself after uninstalling. See `docs/UNINSTALL.md`.

## Third-Party Services

InControl talks to:
- **Ollama** on this PC, or Ollama on a machine you reach over SSH
- **RunPod's API**, only for pod lookup and only if you supplied a key
- **GitHub**, only to download the voice model
- **DuckDuckGo**, and the public pages a model asks to read, only while web search is on

The Microsoft Store installs and updates the app under Microsoft's own privacy statement. InControl does not add an analytics service. A GPU rental is a machine you chose, not an InControl account.

## Children's Privacy

InControl does not knowingly collect any information from children under 13 years of age.

## Changes to This Policy

We may update this privacy policy from time to time. Any changes will be reflected in the "Last Updated" date above.

## Contact

If you have questions about this privacy policy, please open an issue on our GitHub repository:
https://github.com/mcp-tool-shop-org/incontrol/issues

## Your Rights

InControl does not keep an account. Chat history and notes on this PC are yours to delete. Prompts you send while a rental is connected go to that machine, and InControl does not get them back from it.

---

**Summary: InControl collects no account and no telemetry. The chat stays on this PC until you connect a rental, and the app says so while that is true.**
