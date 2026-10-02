# InControl UX Contract

**Version:** 1.0
**Phase:** 3 — UX/UI & Human Experience
**Theme:** Make power legible, calm, and trustworthy

This document defines how InControl speaks, behaves, and presents itself. All UI text, states, and interactions must align with these principles.

---

## User Role Definition

The user is an **operator**, not a "chat user."

An operator:
- Configures and directs execution
- Inspects and understands system behavior
- Manages sessions and artifacts
- Expects transparency and control

InControl is a **local execution environment**, not a chatbot.

---

## Terminology Lock

Use these terms consistently throughout the application:

| Concept | Use This | Not This |
|---------|----------|----------|
| Conversation container | **Session** | Chat, Thread, Conversation |
| AI processing | **Run** / **Execution** | Thinking, Processing, Working |
| AI response | **Model output** | Assistant says, AI response, Reply |
| User input | **Intent** / **Prompt** | Message, Question, Query |
| AI system | **Model** | AI, Assistant, Bot |
| Stopping execution | **Cancel** | Stop, Abort, Kill |
| Error | **Issue** | Error, Problem, Failure |
| GPU/Local | **Device** | Hardware, Machine |
| Saved work | **Session** / **Artifact** | History, Archive |

### Examples

❌ "The assistant is thinking..."
✅ "Running model..."

❌ "Sorry, something went wrong!"
✅ "Execution interrupted. [Reason]. [Action]."

❌ "Start a new chat"
✅ "New session"

❌ "AI response"
✅ "Model output"

---

## UX Principles

### 1. Calm Power

The interface should feel:
- **Dense but not cluttered** — Information is available, not overwhelming
- **Quiet until needed** — Elements appear when relevant
- **Confident** — No hedging, apologizing, or uncertainty in the UI

What this means in practice:
- No bouncing animations or attention-seeking elements
- Status changes are visible but not alarming
- Empty states are opportunities, not failures

### 2. Visible Execution

Users should always know:
- **What is happening** — Current execution stage
- **What will happen** — Predictable next steps
- **What happened** — Clear history and artifacts

What this means in practice:
- Execution stages are labeled, not just "loading"
- Time elapsed is always visible during runs
- Device context (GPU/CPU, memory) is accessible

### 3. No Surprise Waiting

Waiting must feel:
- **Intentional** — "The system is doing work"
- **Bounded** — Progress or time indication
- **Cancellable** — User always has control

What this means in practice:
- Never show a spinner without context
- Always provide a Cancel action during execution
- Show elapsed time, not just "please wait"

### 4. No Blame Language

The interface never:
- Apologizes ("Sorry, we couldn't...")
- Blames the user ("You entered invalid...")
- Uses alarm language ("Error! Failed!")

Instead:
- States facts ("Connection unavailable")
- Provides context ("Model requires more memory than available")
- Offers actions ("Retry with smaller context" / "Select a different model")

---

## Tone Rules by State

### Waiting / Execution

**Tone:** Confident, informative, calm

| Stage | Text |
|-------|------|
| Starting | "Initializing model..." |
| Loading | "Loading [model name]..." |
| Running | "Running inference..." |
| Streaming | "Receiving output..." |
| Finishing | "Completing run..." |

Always show:
- Elapsed time
- Device context
- Cancel action

### Errors / Issues

**Tone:** Direct, helpful, actionable

Pattern:
```
[What happened]
[Why (likely cause)]
[What to do next]
```

Examples:

```
Connection unavailable
The inference backend at localhost:11434 is not responding.
→ Check that Ollama is running
→ Retry connection
```

```
Model not found
"llama3.2" is not available on this device.
→ Pull model with: ollama pull llama3.2
→ Select a different model
```

```
Context limit exceeded
The input exceeds the model's context window (8,192 tokens).
→ Reduce context size
→ Start a new session
```

### Empty States

**Tone:** Welcoming, explanatory, action-oriented

| Location | Text |
|----------|------|
| No sessions | "No sessions yet. Create one to start working." |
| Empty session | "This session is empty. Enter a prompt to begin." |
| No models | "No models available. Pull a model to get started." |
| No context | "No context items attached. Add files or previous outputs." |

### Success / Completion

**Tone:** Minimal, factual

- Don't celebrate ("Great job!")
- Don't over-confirm ("Successfully completed!")
- Just show the result and any relevant metadata

Example:
```
Run complete · 2.3s · 847 tokens
```

---

## Language Patterns

### Action Labels

Use imperative verbs:
- "New session" (not "Create a new session")
- "Cancel" (not "Cancel this run")
- "Copy" (not "Copy to clipboard")
- "Export" (not "Export this session")

### Status Labels

Use present participles or states:
- "Idle" / "Running" / "Complete" / "Issue"
- "Initializing..." / "Loading..." / "Streaming..."
- "Connected" / "Disconnected"

### Time References

Use relative time for recent, absolute for older:
- "Just now" / "2 minutes ago" / "1 hour ago"
- "Yesterday at 3:45 PM"
- "Jan 15, 2026"

---

## Accessibility Commitments

1. **All interactive elements** have visible focus states
2. **All status changes** are announced to screen readers
3. **Motion can be reduced** via system preferences or in-app toggle
4. **Color is never the only indicator** — icons/text accompany color
5. **Keyboard navigation** works for all primary actions

---

## What InControl Is Not

InControl does not:
- Pretend to be human
- Use first-person ("I think...", "I can help...")
- Apologize or express emotions
- Use exclamation points
- Show personality or humor in system UI

The model output may have personality. The application chrome does not.

---

## Revision Process

This document is locked for Phase 3. Changes require:
1. Documented rationale
2. Review of affected UI surfaces
3. Update to this document before implementation

---

## Changelog

| Date | Change |
|------|--------|
| 2026-02-02 | Initial UX contract created for Phase 3 |
