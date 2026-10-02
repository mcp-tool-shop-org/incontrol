# Extensibility Charter

This document defines the rules, boundaries, and guarantees of the InControl-Desktop extension system. **Read this before building or installing any plugin.**

---

## Core Principle

> **Extensions increase capability without reducing trust.**

A plugin must never:
- Bypass operator approval
- Escape the sandbox
- Hide its behavior
- Violate the local-first guarantee

If an extension cannot meet these requirements, it cannot run.

---

## What Is an Extension?

An **extension** (or **plugin**) is a self-contained capability package that:

1. Declares its permissions explicitly in a manifest
2. Registers its capabilities as tools in the Tool Registry
3. Executes within a sandboxed runtime
4. Respects all existing trust boundaries

Extensions are **not**:
- Arbitrary scripts
- Background processes
- System services
- Unrestricted code

---

## What Extensions May Do

### Permitted Capabilities

| Capability | Description | Requires |
|------------|-------------|----------|
| **Read Local Data** | Access operator-approved files/folders | File permission |
| **Write Local Data** | Modify operator-approved locations | File permission + approval |
| **Provide Tools** | Register new assistant tools | Tool declaration |
| **Access Memory** | Read/write to assistant memory | Memory permission |
| **Make Network Requests** | Fetch data from approved endpoints | Network permission + ConnectivityManager |
| **Display UI** | Show configuration or status panels | UI permission |

### Capability Flow

All capabilities flow through existing systems:

```
Extension → Tool Registry → Approval System → Execution → Audit Log
              ↓                    ↓
         Permission Check    Operator Consent
```

No shortcut paths exist.

---

## What Extensions May NEVER Do

### Absolute Prohibitions

These actions are architecturally impossible, not just policy-blocked:

| Prohibition | Reason |
|-------------|--------|
| **Direct file system access** | Must use FileStore with permissions |
| **Direct network access** | Must use ConnectivityManager |
| **Process spawning** | No shell/exec capabilities |
| **Registry/system modification** | Out of scope entirely |
| **Background execution** | No daemon/service model |
| **Silent activation** | Requires explicit operator enable |
| **Bypassing tool approval** | All tools go through approval flow |
| **Accessing other plugins' data** | Isolated storage per plugin |
| **Modifying core application** | Read-only access to app state |
| **Disabling audit logging** | Audit is mandatory and immutable |

### If It's Not Declared, It's Denied

Extensions operate on a **whitelist model**:
- Capabilities must be declared in the manifest
- Undeclared capabilities are rejected at load time
- Runtime requests for undeclared capabilities fail

---

## Risk Classification

Every extension is classified by its maximum risk level:

### Level 1: Read-Only

- Can read approved local data
- Can provide informational tools
- Cannot modify anything
- Cannot access network

**Example**: A tool that searches local documents

### Level 2: Local Mutation

- Everything in Level 1
- Can write to approved local locations
- Can modify assistant memory

**Example**: A tool that organizes files

### Level 3: Network

- Everything in Level 2
- Can make network requests through ConnectivityManager
- Subject to all connectivity rules and audit

**Example**: A tool that fetches weather data

### Level 4: System-Adjacent

- Reserved for future, highly-restricted use cases
- Requires additional operator confirmation
- Not available in Phase 8

**Example**: (None currently permitted)

---

## Operator Control

### The Operator Can Always:

1. **See all installed extensions** — Plugin Manager shows complete list
2. **See what each extension does** — Manifest details visible before install
3. **Disable any extension instantly** — Single toggle, immediate effect
4. **Uninstall any extension** — Complete removal including data
5. **Block categories globally** — "No network extensions" policy
6. **Review extension activity** — Full audit trail in Inspector

### Global Policies

| Policy | Effect |
|--------|--------|
| **Offline Mode** | All network-capable extensions blocked |
| **Read-Only Mode** | All mutation-capable extensions blocked |
| **No Extensions** | All extensions disabled |

These policies override individual extension permissions.

---

## Trust Boundaries

### Extensions Cannot Cross:

```
┌─────────────────────────────────────────────────────┐
│                    CORE APPLICATION                  │
│  ┌───────────────────────────────────────────────┐  │
│  │              TRUST BOUNDARY                    │  │
│  │  ┌─────────────────────────────────────────┐  │  │
│  │  │           EXTENSION SANDBOX              │  │  │
│  │  │                                          │  │  │
│  │  │  Plugin A    Plugin B    Plugin C        │  │  │
│  │  │     │            │           │           │  │  │
│  │  └─────┼────────────┼───────────┼───────────┘  │  │
│  │        │            │           │              │  │
│  │        ▼            ▼           ▼              │  │
│  │   ┌─────────────────────────────────────┐     │  │
│  │   │         MEDIATION LAYER             │     │  │
│  │   │  Tool Registry | FileStore | Network│     │  │
│  │   └─────────────────────────────────────┘     │  │
│  └───────────────────────────────────────────────┘  │
│                         │                            │
│                         ▼                            │
│              ┌─────────────────┐                    │
│              │   AUDIT LOG     │                    │
│              └─────────────────┘                    │
└─────────────────────────────────────────────────────┘
```

### Isolation Guarantees

- **Process isolation**: Extensions run in restricted context
- **Data isolation**: Each extension has private storage
- **Failure isolation**: Crashed extension cannot crash app
- **Permission isolation**: Capabilities are non-transferable

---

## Manifest Requirements

Every extension must provide a manifest declaring:

```yaml
id: unique-identifier
version: semantic-version
name: Human Readable Name
author: Author Name
description: What this extension does

permissions:
  - file:read:/documents
  - memory:read
  - network:https://api.example.com

capabilities:
  - tool:search-documents
  - tool:summarize-file

risk_level: 2  # Local Mutation

network_intent:
  endpoints:
    - https://api.example.com
  data_sent:
    - search queries
  data_received:
    - search results
  retention: session
```

### Manifest Validation

At load time, the system validates:

1. All required fields present
2. Permissions are specific (no wildcards for sensitive resources)
3. Network intent matches declared endpoints
4. Risk level matches requested permissions
5. No prohibited capabilities requested

Invalid manifests are rejected with clear error messages.

---

## Installation Requirements

### Before Installation

The operator sees:
- Extension name, author, version
- Complete permission list
- Network intent (if any)
- Risk level classification
- Storage requirements

### Installation Consent

The operator must:
1. Review the manifest summary
2. Explicitly confirm installation
3. Choose to enable or leave disabled

### No Silent Installation

- Extensions cannot install themselves
- Extensions cannot install other extensions
- No "required dependencies" that auto-install
- No "recommended extensions" that auto-enable

---

## Audit Requirements

### Every Extension Action Is Logged

The audit log captures:
- Which extension acted
- What action was taken
- What inputs were provided
- What outputs were produced
- Timestamp and duration
- Success or failure

### Audit Cannot Be Disabled

- Extensions cannot suppress their own logging
- Extensions cannot read the audit log
- Audit persists even if extension is uninstalled

### Inspector Integration

All extension activity appears in the existing Inspector:
- Unified view with built-in tools
- Filter by extension
- Full trace detail

---

## Disabling Extensions

### Disable vs Uninstall

| Action | Effect |
|--------|--------|
| **Disable** | Extension stops running, data preserved |
| **Uninstall** | Extension removed, data deleted |

### Disable Is Instant

When an extension is disabled:
1. All its tools are unregistered immediately
2. No new actions can be initiated
3. In-progress actions complete but log a warning
4. Extension cannot re-enable itself

### Global Disable

The "No Extensions" policy disables all extensions with one toggle.

---

## What This Charter Does NOT Permit

The following are explicitly out of scope for Phase 8:

1. **Remote extension marketplace** — Local install only
2. **Auto-update of extensions** — Manual update required
3. **Extension-to-extension communication** — Isolated by design
4. **Custom UI frameworks** — Limited UI surface
5. **Background tasks** — No persistent execution
6. **System integration** — No OS-level hooks

These may be considered in future phases with additional safeguards.

---

## Answering Key Questions

### "What can plugins do?"

Plugins can provide new tools that:
- Read approved local files
- Write to approved locations
- Access assistant memory
- Make approved network requests
- Display configuration UI

All through existing, audited channels.

### "How do I disable all plugins?"

Settings → Extensions → "Disable All Extensions"

One toggle. Immediate effect. No exceptions.

### "Can a plugin access my files without permission?"

No. File access requires:
1. Declared in manifest
2. Approved at install
3. Mediated through FileStore
4. Logged in audit

### "Can a plugin connect to the internet silently?"

No. Network access requires:
1. ConnectivityManager not in OfflineOnly mode
2. Network permission declared in manifest
3. Endpoint approved in connectivity settings
4. Request logged in audit

### "What happens if a plugin crashes?"

The plugin is isolated. The crash is logged. The app continues running. The extension is marked as failed. You can disable or uninstall it.

### "Can I trust extensions from others?"

Extensions are verifiable:
- Signed packages with checksums
- Clear provenance in manifest
- All behavior auditable after install

Trust is based on transparency, not faith.

---

## Summary

The InControl-Desktop extension system is designed around one principle:

> **Power with accountability.**

Extensions can do useful things. They cannot do hidden things. The operator always knows what's installed, what it can do, and what it has done.

If this charter is followed, extensions make InControl more capable without making it less trustworthy.

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-03 | Initial charter |
