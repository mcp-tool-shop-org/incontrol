# Privacy Policy for InControl-Desktop

**Last Updated: February 3, 2026**

## Overview

InControl is an Ollama chat app for Windows. The chat runs on this PC until you connect a GPU you rented over SSH. That choice is shown on the chat, and it is separate from any tool-URL permission.

## Data Collection

**InControl-Desktop does not collect, transmit, or store any personal data on external servers.**

### What stays on your device:
- All conversations and chat history
- Your settings and preferences
- AI model files
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

InControl-Desktop may make the following optional network connections:

1. **Ollama Model Downloads** - When you choose to download AI models, the app connects to Ollama's model registry. This is initiated only by your explicit action.

2. **Update Checks** - If enabled, the app may check for updates through the Microsoft Store infrastructure.

Those connections are not the chat. The chat is transmitted only while a rental is connected, and then only to that machine.

## Data Storage

All application data is stored locally in:
- `%LOCALAPPDATA%\InControl\` - Application settings and logs
- Ollama's default model storage location

You can delete all application data by uninstalling the app and removing the above folder.

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

Since we don't collect any personal data, there is no personal data to access, correct, or delete. All your data remains on your device under your control.

---

**Summary: InControl collects no account and no telemetry. The chat stays on this PC until you connect a rental, and the app says so while that is true.**
