# Phase 11 Commit 6 — Error UX Pass: Calm, Actionable Recovery

## Summary
Enhanced the error display pattern to be calm, factual, and actionable. Added "What to try" suggestions section to IssueCard and improved factory methods with helpful recovery hints.

## UX Philosophy
Per the design contract:
- **No blame language** - State facts, not accusations
- **Calm visual tone** - Not alarming red unless truly critical
- **Always offer next steps** - Users should know what to do
- **Suggestions over errors** - "What to try" instead of "What went wrong"

## Changes Made

### IssueCard.xaml
- Added SuggestionsPanel with "What to try:" header
- Bullet-pointed suggestion list using ItemsControl
- Uses secondary/tertiary foreground for calm appearance
- Ghost button style for dismiss (less intrusive)
- Added fourth row to Grid for suggestions

### IssueCard.xaml.cs
- Added logic to show/hide SuggestionsPanel
- Binds SuggestionsList.ItemsSource to issue.Suggestions

### IssueViewModel.cs
- Added `Suggestions` list property
- Added `HasSuggestions` property
- Added `WithSuggestion(string)` fluent method
- Added `WithSuggestions(params string[])` fluent method
- Updated factory methods with helpful suggestions:

## Factory Method Improvements

### ConnectionUnavailable
**Suggestions:**
- Check if the backend service is running
- Verify the endpoint address is correct
- Check your network connection

### ModelNotFound
**Suggestions:**
- The model may need to be downloaded first
- Check if the model file was moved or renamed

### ContextLimitExceeded
**Suggestions:**
- Remove some context attachments
- Shorten the conversation history
- Use a model with a larger context window

### OutOfMemory
**Title changed:** "Insufficient GPU memory" (more specific)
**Suggestions:**
- Close other GPU-intensive applications
- Use a quantized (smaller) version of the model
- Select a model with fewer parameters

### ExecutionInterrupted
**Suggestions:**
- This may be a temporary issue. Try running again.

### New: OperationFailed
Generic factory for failed operations with log suggestion.

## Visual Design
```
┌────────────────────────────────────────────┐
│ ⚠ Connection unavailable              [×] │
├────────────────────────────────────────────┤
│ The inference backend at localhost:8080    │
│ is not responding.                         │
├────────────────────────────────────────────┤
│ What to try:                               │
│ • Check if the backend service is running  │
│ • Verify the endpoint address is correct   │
│ • Check your network connection            │
├────────────────────────────────────────────┤
│ [Retry connection]  [Check backend status] │
└────────────────────────────────────────────┘
```

## Build Verification
- Clean build: **0 errors, 0 warnings**
- Runtime: win-x64

## Files Changed
- `src/InControl.App/Controls/IssueCard.xaml`
- `src/InControl.App/Controls/IssueCard.xaml.cs`
- `src/InControl.ViewModels/Errors/IssueViewModel.cs`
