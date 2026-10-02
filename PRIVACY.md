# Privacy Policy for InControl

**Last Updated: October 2, 2026**

## Overview

InControl is an Ollama chat app for Windows. The chat runs on this PC until you connect a GPU you rented over SSH. That choice is shown on the chat, and it is separate from any tool-URL permission.

## Data Collection

**InControl does not run an account service and does not collect telemetry.** A chat on this PC stays on this PC. If you connect a GPU rental, the prompts you send are transmitted to Ollama on that machine through SSH.

### What stays on your device:
- Conversations and chat history, until you connect a rental
- Your settings and preferences
- The private key file you choose (it is not uploaded)
- Session data

### What we do NOT collect:
- Personal information
- Usage analytics
- Conversation content
- Telemetry data
- Location data
- Device identifiers

## Local Processing

By default, inference is Ollama on this PC and the conversation stays here. If you connect a rental, the prompts you send go to Ollama on that machine through an SSH port forward. InControl does not run its own cloud, does not require an account, and does not sync a profile.

## Network Connections

When you ask it to, InControl may also:

1. **Pull a model** through Ollama's registry. That happens only when you start a pull, and it uses the Ollama the banner is naming.
2. **Look up RunPod pods** that are already running, if `RUNPOD_API_KEY` is in the environment. Lookup does not start a pod and does not store the key.

There is no Microsoft Store update check in 0.3.0. The offline switch turns off a rental, RunPod lookup, and model download. It does not claim to block every socket. Chat on this PC still works while it is on.

The chat itself is transmitted only while a rental is connected, and then only to that machine.

## Data Storage

All application data is stored locally in:
- `%LOCALAPPDATA%\InControl\` - Application settings and logs
- Ollama's default model storage location

You can delete application data on this PC by removing that folder. See `docs/UNINSTALL.md`. There is no MSIX to uninstall.

## Third-Party Services

InControl-Desktop integrates with:
- **Ollama** on this PC, or Ollama on a machine you reach over SSH

InControl does not add an analytics service. A GPU rental is a machine you chose, not an InControl account.

## Children's Privacy

InControl-Desktop does not knowingly collect any information from children under 13 years of age.

## Changes to This Policy

We may update this privacy policy from time to time. Any changes will be reflected in the "Last Updated" date above.

## Contact

If you have questions about this privacy policy, please open an issue on our GitHub repository:
https://github.com/mcp-tool-shop-org/incontrol/issues

## Your Rights

InControl does not keep an account. Chat history on this PC is yours to delete. Prompts you send while a rental is connected go to that machine, and InControl does not get them back from it.

---

**Summary: InControl collects no account and no telemetry. The chat stays on this PC until you connect a rental, and the app says so while that is true.**
