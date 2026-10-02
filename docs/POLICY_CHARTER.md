# Policy Charter — InControl-Desktop

## Purpose

This document defines how policy governs InControl-Desktop behavior. Policy provides **controllable, auditable, and deployable** governance for individuals, teams, and managed environments — while keeping the human experience calm and operator-first.

## Core Principles

### 1. Explicit Over Implicit

Every restriction must be declared. No hidden limitations. No surprise blocks.

```
✓ Tool blocked by: Org Policy > tool.allow[weather-only]
✗ Tool unavailable (no explanation)
```

### 2. Layered With Clear Precedence

Policies compose predictably. Higher authority always wins.

**Precedence Order (highest to lowest):**
1. **Org Policy** — Enterprise/organization-wide controls
2. **Team Policy** — Workgroup or department settings
3. **User Policy** — Individual operator preferences
4. **Session Policy** — Temporary overrides (most permissive only)

When policies conflict, the more restrictive rule at the higher level wins.

### 3. Auditable

Every policy decision is logged. Every block has an explanation.

```
PolicyDecision {
    Subject: "tool:internet-search"
    Action: "Deny"
    Reason: "Blocked by Org Policy: internet-tools.allowed = false"
    Source: "C:\ProgramData\InControl\policy.json"
    Timestamp: "2025-02-03T10:30:00Z"
}
```

### 4. Deterministic

Same input, same output. No probabilistic policy evaluation.

- Policy evaluation is synchronous and pure
- Cache invalidation is explicit
- Order of rule evaluation is defined

### 5. Human-First

Policy serves operators, not the other way around.

- Blocks are explained, not cryptic
- Approval prompts are minimal and consistent
- No modal fatigue from constant interruptions

## Enforcement Points

Policy governs these domains:

### Tools

| Control | Description |
|---------|-------------|
| `allow/deny` | Enable or disable specific tools by ID |
| `require_approval` | Require operator consent before execution |
| `input_constraints` | Restrict what inputs tools can receive |
| `output_redaction` | Filter sensitive data from tool outputs |
| `rate_limits` | Maximum invocations per time window |
| `domain_allowlist` | Restrict network tools to specific domains |

### Plugins

| Control | Description |
|---------|-------------|
| `allowlist/denylist` | Control which plugins can load |
| `publisher_trust` | Trust or block by publisher |
| `signature_required` | Require signed packages |
| `permission_caps` | Maximum permissions plugins can request |
| `quarantine` | Disable plugin pending review |

### Memory

| Control | Description |
|---------|-------------|
| `enabled` | Enable or disable memory entirely |
| `scope` | Session-only vs persistent |
| `retention_days` | Auto-purge after N days |
| `types_allowed` | Preferences only, no facts, etc. |
| `export_required` | Require export before purge |

### Connectivity

| Control | Description |
|---------|-------------|
| `mode_allowed` | Restrict to Offline/Assisted/Connected |
| `mode_locked` | Prevent user from changing mode |
| `domain_allowlist` | Restrict outbound domains |
| `per_tool_network` | Control network per tool |

### Updates

| Control | Description |
|---------|-------------|
| `enabled` | Enable or disable updates |
| `mode` | None/Notify/Manual/Auto |
| `channel` | Stable/Beta/Dev |
| `schedule` | When updates can occur |

## Policy Sources

### File-Based Policy

Primary policy location for managed environments:

```
Windows:  %ProgramData%\InControl\policy.json
macOS:    /Library/Application Support/InControl/policy.json
Linux:    /etc/incontrol/policy.json
```

User policy (lower precedence):

```
Windows:  %LOCALAPPDATA%\InControl\user-policy.json
macOS:    ~/Library/Application Support/InControl/user-policy.json
Linux:    ~/.config/incontrol/user-policy.json
```

### Policy Lock

When `policy.lock = true` in the org policy:
- User cannot override org settings
- UI shows "Managed by Organization" indicators
- Policy file changes require admin privileges

### Signature Verification

Optional but recommended for enterprise:
- Policy files can be signed
- App verifies signature before loading
- Tampered policies are rejected with clear error

## Decision Types

| Decision | Meaning |
|----------|---------|
| `Allow` | Action permitted without restriction |
| `Deny` | Action blocked, explain why |
| `AllowWithApproval` | Action permitted after operator confirms |
| `AllowWithConstraints` | Action permitted with restrictions applied |

## Policy Evaluation Flow

```
Request (tool execution, plugin load, etc.)
    │
    ▼
┌─────────────────────┐
│  Load Policy Chain  │  (Org → Team → User → Session)
└─────────────────────┘
    │
    ▼
┌─────────────────────┐
│  Find Matching Rules│  (most specific rule wins)
└─────────────────────┘
    │
    ▼
┌─────────────────────┐
│  Apply Precedence   │  (higher source wins conflicts)
└─────────────────────┘
    │
    ▼
┌─────────────────────┐
│  Return Decision    │  (Allow/Deny/Approval/Constrained)
│  + Explanation      │
└─────────────────────┘
    │
    ▼
┌─────────────────────┐
│  Audit Log Entry    │
└─────────────────────┘
```

## Human-Experience Guarantees

### 1. Operator Can Tell Which Policy Is In Effect

The app provides:
- Policy viewer showing effective (merged) policy
- Source attribution ("From Org Policy")
- Conflict resolution explanation

### 2. Operator Can Tell Why An Action Is Blocked

Every denial includes:
- What was blocked
- Which rule blocked it
- Where the rule came from
- What to do about it (if anything)

### 3. Policy Never Feels Random

- Same request, same answer
- No time-based surprises
- No hidden state affecting decisions
- Approval prompts are consistent

## Non-Goals

### Not a Cloud Service

Policy is local files, not cloud-managed (for now).
Enterprise can deploy via MDM, GPO, or config management.

### Not User Authentication

Policy doesn't identify users.
User policy is "whoever is running the app."

### Not DRM

Policy doesn't prevent determined circumvention.
It provides governance for cooperative environments.

## Schema Version

This charter defines Policy Schema v1.0.
Future versions will maintain backward compatibility.

## Summary

Policy in InControl-Desktop is:

- **Explicit**: Every restriction is declared
- **Layered**: Org > Team > User > Session
- **Auditable**: Every decision is logged
- **Deterministic**: Same input, same output
- **Human-First**: Blocks are explained, not cryptic

The goal is governance that **reduces anxiety** — operators always know what's happening and why.
