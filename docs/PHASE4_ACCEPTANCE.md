# Phase 4 Acceptance Gate

**InControl-Desktop — Operational Hardening & Distribution**

## Phase 4 Objective

Ship a desktop product that installs cleanly, updates safely, survives failure, and can be supported without tribal knowledge.

**No new product features.**
Only: packaging, stability, recoverability, diagnostics, distribution, and release discipline.

---

## Gate 1 — Reproducible Release Build (ENFORCED)

### Requirements

- [ ] `scripts/verify.ps1` passes on clean clone
- [ ] `scripts/release.ps1` produces release artifacts in a deterministic folder layout
- [ ] Release build uses `Release` configuration (not Debug)
- [ ] Version is embedded in:
  - [ ] app UI (About or footer)
  - [ ] logs/diagnostics output
- [ ] Build outputs are bitwise reproducible or you document exactly why not (timestamps/signing)

### Evidence

- [ ] CI log showing success
- [ ] Artifact folder snapshot
- [ ] Version string captured from UI + logs

---

## Gate 2 — Installer Packaging & Lifecycle (HUMAN-VERIFIED + ENFORCED WHERE POSSIBLE)

### Requirements

- [ ] Installer exists (MSIX) and can:
  - [ ] install
  - [ ] uninstall
  - [ ] repair (if supported)
- [ ] Upgrade flow tested:
  - [ ] install vA → upgrade to vB
  - [ ] settings persist
  - [ ] sessions persist
  - [ ] no duplicate app entries
- [ ] App launches successfully after:
  - [ ] fresh install
  - [ ] upgrade
  - [ ] reboot
- [ ] No admin rights required (unless documented as required)

### Evidence

- [ ] "Clean machine" smoke checklist (can be a VM)
- [ ] Screenshots of install/uninstall + version before/after upgrade

---

## Gate 3 — Data Locations & Write Boundaries (ENFORCED)

### Requirements

- [ ] All data paths are explicit and documented:
  - [ ] where sessions live
  - [ ] where logs live
  - [ ] where cache live
  - [ ] where exports live
- [ ] No writes occur outside allowed roots
- [ ] "Reveal on disk" actions open correct paths
- [ ] Storage failures are handled with user-safe recovery (no crash loops)

### Evidence

- [ ] Automated tests for allowed/forbidden paths
- [ ] Doc section listing paths + purpose

---

## Gate 4 — Crash Resilience & Recovery (ENFORCED + HUMAN-VERIFIED)

### Requirements

- [ ] App does not corrupt state on:
  - [ ] forced close during execution
  - [ ] power loss simulation (kill process)
- [ ] On next launch:
  - [ ] app starts
  - [ ] session list loads
  - [ ] partial run is marked clearly (not "successful")
- [ ] Corrupt state handling:
  - [ ] detect corrupt file(s)
  - [ ] offer recovery actions:
    - [ ] repair/rebuild index
    - [ ] restore last good snapshot (if supported)
    - [ ] quarantine corrupt session
    - [ ] reset app (with export-first prompt)

### Evidence

- [ ] Tests for corrupt JSON / missing files
- [ ] Manual kill-and-relaunch checklist results

---

## Gate 5 — Performance & Responsiveness Budget (ENFORCED)

### Requirements (targets for baseline machine: RTX GPU, 16GB+ RAM)

- [ ] Cold start < 3 seconds
- [ ] Session switch < 100ms for 50 sessions
- [ ] Conversation render remains responsive at:
  - [ ] 1k messages
  - [ ] 10k messages (may degrade gracefully but must not freeze)
- [ ] Execution cancel returns UI to idle within 500ms
- [ ] No unbounded memory growth across repeated runs

### Evidence

- [ ] Benchmarks or perf tests in CI (even minimal)
- [ ] Memory trend captured in diagnostics or logs

---

## Gate 6 — Observability & Support Bundle (ENFORCED)

### Requirements

- [ ] Structured logs exist with levels/categories
- [ ] Logs are bounded (rolling files + size caps)
- [ ] "Copy Diagnostics" includes:
  - [ ] version
  - [ ] runtime/OS info
  - [ ] execution state
  - [ ] device/model selection
  - [ ] last error codes (no stack traces by default)
- [ ] "Export Support Bundle" produces a zip with:
  - [ ] logs
  - [ ] health report
  - [ ] minimal config (sanitized)
  - [ ] session metadata (optional, user-approved)

### Evidence

- [ ] Unit tests validating bundle contents
- [ ] Manual validation: bundle opens and is readable

---

## Gate 7 — Security & Privacy Baseline (ENFORCED + DOCS)

### Requirements

- [ ] Explicit statement: what data is stored locally
- [ ] Explicit statement: what network calls occur (if any)
- [ ] Secrets policy:
  - [ ] no secrets in logs
  - [ ] any tokens stored using OS-appropriate secure storage
- [ ] Dependency scan is run (best-effort OK):
  - [ ] report produced
  - [ ] known criticals addressed or documented

### Evidence

- [ ] `docs/PRIVACY.md` or section in README
- [ ] Scan output artifact

---

## Gate 8 — Export/Import & Data Portability (ENFORCED)

### Requirements

- [ ] Export session(s) to:
  - [ ] markdown (human)
  - [ ] JSON (machine)
- [ ] Import restores a session without breaking indexing
- [ ] "Reset app" flow exists with:
  - [ ] export-first recommendation
  - [ ] explicit confirmation
  - [ ] post-reset onboarding

### Evidence

- [ ] End-to-end tests: `export → reset → import → verify session`

---

## Gate 9 — Release Discipline & Documentation (HUMAN-VERIFIED)

### Requirements

- [ ] README includes:
  - [ ] what InControl-Desktop is
  - [ ] install/run
  - [ ] build/test
  - [ ] troubleshooting
- [ ] `docs/TROUBLESHOOTING.md` exists:
  - [ ] common GPU issues
  - [ ] model missing/unavailable
  - [ ] corrupted state recovery
- [ ] "How to add a new diagnostic" + "how to report a bug" documented
- [ ] Issue templates exist (if public repo)

### Evidence

- [ ] Docs reviewed by someone "cold" (even you on a clean VM)

---

## Gate 10 — Final Go/No-Go Checklist (FINAL)

### Requirements

- [ ] All ENFORCED gates pass in CI
- [ ] Smoke test runbook executed on clean machine
- [ ] Install/upgrade/uninstall confirmed
- [ ] Support bundle tested
- [ ] No crash loop scenarios known
- [ ] Release notes written
- [ ] Artifacts uploaded/published per distribution plan

### Evidence

- [ ] Completed runbook + artifact links

---

## Phase 4 Completion Definition

Phase 4 is complete when:

1. Every **ENFORCED** gate is mechanically verifiable
2. Every **HUMAN-VERIFIED** gate has recorded evidence
3. A clean machine can install, run, recover from failure, and export diagnostics **without developer intervention**
