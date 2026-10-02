# Connectivity

Two different choices live near this word. They are not the same switch.

**Where the chat runs.** Ollama on this PC is the default, at `http://127.0.0.1:11434`. A GPU you rented is an SSH local forward. The bar across the top names that machine while prompts leave this PC. When SSH exits, the chat comes back here.

**The offline switch.** While it is on, InControl will not open a rented GPU, look up RunPod, or download a model. A rental that is already open is closed. Chat with Ollama on this PC still works. The switch does not block web search, app updates, or extension network calls, and the Connectivity page does not say that it does.

**Tool URLs.** Allowing a tool URL does not send the chat to a rented GPU. Connecting a rental does not change the tool allowlist.

There is no MSIX in this repository and no mode that stops every socket. Version 2.0.0 is a source build. A later package identity `InControl.App` `2.0.0.0` is being prepared. Tag `v0.3.0` is an older source-only release. See `docs/INSTALLATION.md`.
