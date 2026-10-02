# Where the chat runs

InControl talks to Ollama. The default is Ollama on this PC at `http://127.0.0.1:11434`. Prompts stay here.

A rented GPU is optional. You connect it yourself. While it is connected, the bar on the chat says which machine has the chat, and that prompts leave this PC. **Stay on this PC** drops the tunnel and the chat is local again. There is no warning on every message.

This is not the Connectivity page. That page is an allowlist for assistant tool calls. Allowing a URL there does not send the chat anywhere.

## What you paste

The rental's direct SSH command:

```text
ssh -p 17432 root@203.0.113.10 -i C:\keys\id_ed25519
```

| Field | What it is |
| --- | --- |
| User, host, `-p` | The sshd login. The port is almost never 22, and it is never 11434. |
| `-i` | Private key file on this PC. InControl does not upload it. |
| Remote Ollama port | `11434` on the rental's loopback, unless you moved Ollama. |

On the rental:

```bash
ollama serve
```

Ollama's own default bind is `127.0.0.1:11434`. Leave it there. `OLLAMA_HOST=0.0.0.0` and a published `11434` are an open inference API with no login. InControl will not do that for you.

## What the app opens

An OpenSSH local forward, and nothing else:

```text
ssh -F <config> -N incontrol-compute
```

The config binds `127.0.0.1` on this PC to `127.0.0.1:11434` on the rental. The chat is still normal Ollama HTTP, aimed at the local end of that forward. There is no remote command, so the prompt is not a shell argument. Agent forwarding is off. The first connection stores the host key. A changed key fails closed. Forget the saved host key only when you mean to trust the new machine.

The forward uses numeric `127.0.0.1` on both sides. Windows OpenSSH can resolve the name `localhost` to IPv6 and then miss an IPv4 listener.

## Rentals

The dial is the same record for every provider: user, host, sshd port, identity file. InControl does not call a provider API. You paste the command the console shows, and you paste a new one after a restart. Saved host and port do not survive a reset.

**RunPod.** The proxy user at `ssh.runpod.io` is an interactive shell. It cannot forward a port, so InControl refuses it. Use the direct line: `root` at the public IP and the mapped port. That line is absent while the pod is stopped, and the mapped port changes on reset. Community Cloud may also change the IP. The HTTP proxy in front of the pod closes long streams. It is not the chat path.

**Vast.ai.** The documented forward is the direct public-IP SSH, with `-L`. The `sshNNN.vast.ai` proxy is not documented for that. InControl warns and still tries. If the tunnel fails, paste the direct line. A stopped instance may not come back on the same GPU.

**Anyone else with sshd.** Same fields. The machine must allow local TCP forwarding to `127.0.0.1:11434`.

## When it fails

| What you see | What it means |
| --- | --- |
| RunPod proxy message | You pasted `ssh.runpod.io`. Use the direct public-IP command. |
| Port 11434 refused as the SSH port | That number is Ollama, not sshd. Use the port from the ssh command. |
| Host key changed | The rental was rebuilt, or this is a different machine. Forget the saved key only if you trust it. |
| Tunnel is up, Ollama is not answering | SSH worked. Start `ollama serve` on the rental. Do not publish the port. |
| Address stops working after a restart | Expected. Paste the new command. |
