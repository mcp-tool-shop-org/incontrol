# InControl-Desktop Release Charter

## Purpose

This document defines the release and trust boundary for InControl-Desktop. It establishes what "release" means, what infrastructure is in scope, and what is explicitly forbidden.

**Governing principle:** Nothing connects, updates, or changes itself without the operator understanding what, why, and how to undo it.

---

## What "Release" Means

A release is:
- A versioned, signed, verifiable build of InControl-Desktop
- Produced by automated CI from a tagged commit
- Published to documented distribution channels
- Installable without developer assistance

A release is NOT:
- A development build
- An unsigned binary
- A "just run this script" artifact
- Something that changes behavior after installation

---

## Trust Envelope

### Local-First by Default

InControl-Desktop operates fully offline unless the operator explicitly enables connectivity.

| Principle | Guarantee |
|-----------|-----------|
| **Offline-safe** | The application functions completely without internet access |
| **No phone-home** | No telemetry, analytics, or crash reporting without explicit consent |
| **Local storage** | All user data stored on the operator's device |
| **No cloud dependency** | No features require cloud services to function |

### Explicit Consent for Updates

Updates happen **only** when the operator chooses:

1. **Manual mode (default)**: Operator downloads and installs updates themselves
2. **Notify-only mode**: Application notifies of available updates, operator decides
3. **Auto-update mode**: Operator explicitly opts in; can revert at any time

### Explicit Consent for Connectivity

Internet connectivity is:
- **Disabled by default** (OfflineOnly mode)
- **Enabled only through explicit operator action**
- **Revocable at any time** ("go offline now" button)
- **Auditable** (all network activity logged)

---

## Infrastructure Scope

### In Scope

| Component | Purpose |
|-----------|---------|
| CI/CD pipeline | Automated, repeatable builds |
| Code signing | Cryptographic verification |
| Version management | Deterministic versioning |
| MSIX packaging | Windows-native installation |
| Artifact storage | Release distribution |
| Update manifest | Version discovery |

### Out of Scope

| Component | Reason |
|-----------|--------|
| Cloud services | Local-first principle |
| User accounts | No login required |
| Remote configuration | No external control |
| Analytics/telemetry | Privacy by default |
| Background services | Operator-visible only |

---

## Non-Goals (Explicitly Forbidden)

### No Silent Updates
- Updates NEVER install without operator action
- The application NEVER modifies itself in the background
- Version changes are ALWAYS visible before they happen

### No Background Network Activity
- No periodic "check-in" calls
- No heartbeat to any server
- No "phone home" for any reason without consent
- Offline mode means truly offline

### No Auto-Enablement of Internet Features
- Connectivity features start disabled
- No "we enabled this for your convenience"
- No "this feature requires internet" nagging
- Offline experience is complete, not degraded

### No External Control
- No remote kill switch
- No remote configuration changes
- No "we updated the terms" enforcement
- The operator's installed version is sovereign

---

## Operator Questions (Must Be Answerable)

### "Who controls updates?"

**Answer:** You do.
- Default mode: You download and install manually
- Notify mode: We tell you updates exist; you decide
- Auto mode: Only if you explicitly enable it
- You can always decline or roll back

### "What talks to the internet?"

**Answer:** Nothing, unless you enable it.
- Default: Completely offline
- If enabled: Only features you explicitly approve
- Every network request is logged and auditable
- One button to go offline immediately

### "How do I turn this off?"

**Answer:**
- **Updates:** Settings → Updates → Mode → Manual
- **Connectivity:** Settings → Connectivity → Offline Only
- **Everything:** Uninstall removes the application cleanly

---

## Version Policy

### Versioning Scheme

```
MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
```

| Component | Meaning |
|-----------|---------|
| MAJOR | Breaking changes to user data or settings |
| MINOR | New features, backward compatible |
| PATCH | Bug fixes, no feature changes |
| PRERELEASE | alpha, beta, rc (optional) |
| BUILD | CI build number (optional) |

### Version Source of Truth

Version is defined in:
```
src/InControl.App/InControl.App.csproj
```

CI reads this value. No manual version management in CI.

---

## Signature Policy

### Signing Requirements

| Artifact | Signed | Required for Release |
|----------|--------|---------------------|
| MSIX package | Yes | Yes |
| Unsigned build | No | Never released |

### Certificate Requirements

- Extended Validation (EV) code signing certificate preferred
- Standard code signing certificate minimum
- Certificate details documented in release notes

### Verification

Operators can verify:
1. Windows SmartScreen shows publisher name
2. Right-click → Properties → Digital Signatures
3. SHA256 checksum published with release

---

## Rollback Policy

### Supported Rollback

- Previous version installer always available
- Settings preserved during downgrade when possible
- User data never deleted during rollback

### Rollback Limitations

- Cannot downgrade across MAJOR versions (data format changes)
- Explicit warning before rollback with data implications
- Clean install always available as fallback

---

## Data Policy During Updates

### Never Touched
- User documents
- Memory items (unless explicitly migrated)
- Personal settings

### Migrated When Necessary
- Configuration format (with backup)
- Database schema (with rollback option)

### Clearly Communicated
- What will change
- What will be backed up
- How to recover if something goes wrong

---

## Support Commitment

### For Stable Releases
- Security fixes for current and previous MINOR version
- Critical bugs fixed promptly
- Migration guides for MAJOR upgrades

### For Pre-releases
- No stability guarantee
- Testing feedback welcome
- Not for production use

---

## Audit Checkpoints

Before any release:

- [ ] Version matches tag
- [ ] All tests pass
- [ ] Package is signed
- [ ] Checksum is published
- [ ] Install tested on clean VM
- [ ] Upgrade from previous version tested
- [ ] Uninstall tested (no orphan files)
- [ ] Offline mode verified
- [ ] No unexpected network calls

---

## Charter Amendments

This charter can only be amended through:
1. Pull request with clear justification
2. Review period (minimum 7 days for significant changes)
3. Explicit documentation of what changed and why

Changes that reduce operator control require:
- Strong justification
- Opt-out path
- Migration documentation

---

*This document is the source of truth for release behavior. If the code contradicts this document, the code is wrong.*
