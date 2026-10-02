---
title: Where the chat runs
description: Ollama on this PC by default, or a GPU you rented, over SSH.
sidebar:
  order: 2
---

The default is Ollama on this PC at `http://127.0.0.1:11434`. Prompts stay here. Nothing is sent to a rental until you connect one.

Settings has a section named **Where this chat runs**. The connect button says the chat will be sent to that machine. While it is connected, a bar on the chat names the machine. **Stay on this PC** drops the tunnel. There is no warning on every message.

This is not the Connectivity page. That page is an allowlist for assistant tool calls.

## What you paste

A direct SSH command:

```text
ssh -p 17432 root@203.0.113.10 -i C:\keys\id_ed25519
```

The port in that command is sshd. It is never 11434. 11434 is Ollama on the rental's loopback. Leave it there. Do not set `OLLAMA_HOST=0.0.0.0` and do not publish port 11434.

## What the app opens

An OpenSSH local forward and nothing else. On this PC the forward listens on `127.0.0.1:11436`. The chat stays here until Ollama's `/api/version` answers on that port. An open TCP port is not enough. 11436 is not 11434, so a dead tunnel cannot be answered by Ollama on this PC.

There is no remote command. The prompt is not a shell argument. Agent forwarding is off. Each login keeps its own host-key file. A changed key fails closed. InControl does not edit your SSH config. The private key stays on this PC.

## RunPod

**Look up my RunPod pods** reads `RUNPOD_API_KEY` from the environment and fills the direct command for a pod that is already running: `root` at the public IP, and the host port mapped to container port 22. The key is not stored. Lookup does not create, start, or stop a pod, and it does not send the chat. You still press Connect.

`ssh.runpod.io` is refused. That proxy is a shell and cannot forward a port. A pod that publishes 11434 is refused too.

The address dies when the rental restarts. Look the pod up again, or paste the new command.

## Anyone else with sshd

Paste works for Vast and for any other sshd that allows a local forward to `127.0.0.1:11434`. Vast's `sshNNN.vast.ai` proxy is warned about. The documented forward is the direct public-IP SSH.

The rules in the repository file `docs/COMPUTE.md` are the longer version of this page.
