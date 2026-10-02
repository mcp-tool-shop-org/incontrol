# Phase 11 Commit 5 — Disabled-State & 'Why' Explanations Everywhere

## Summary
Enhanced disabled controls with clear explanations of why they're disabled and what the user needs to do to enable them. Every disabled button now has a tooltip or banner explaining the situation.

## Problem Solved
Previously:
- Disabled buttons showed no explanation
- Users didn't know why a control was unavailable
- No guidance on how to enable disabled functionality

## Changes Made

### InputComposer.xaml
- Added tooltip to Run button: shows dynamic reason when disabled
- Added tooltip to "Clear all context" menu item: "No context items to clear"

### InputComposer.xaml.cs
- Added `UpdateTooltips()` method that dynamically updates button tooltips
- Tooltip changes based on state:
  - "Select a model to enable" when no model selected
  - "Type a prompt to enable" when text is empty
  - "Blocked by connectivity policy" when offline
  - "Waiting for current operation" when executing
  - "Run inference (Ctrl+Enter)" when enabled
- Clear context tooltip: "Remove all context items" when enabled, "No context items to clear" when disabled

### ExtensionsPage.xaml
- Added tooltip to extension toggle switch: "Enable or disable this extension"
- Added OnContent="On" and OffContent="Off" labels to toggle

## Disabled State Banner Pattern
The InputComposer already implements the ideal pattern (established in earlier commits):
- Shows a warning banner below the input area
- Displays clear reason text
- Optionally shows an action button (e.g., "Open Model Manager")
- Banner appears only when Run is disabled
- Hides keyboard hint when banner is shown

## Tooltip Hierarchy
| Control | Enabled Tooltip | Disabled Tooltip |
|---------|-----------------|------------------|
| Run Button | "Run inference (Ctrl+Enter)" | Dynamic reason |
| Clear Context | "Remove all context items" | "No context items to clear" |
| Extension Toggle | "Enable or disable this extension" | N/A (always works) |

## Build Verification
- Clean build: **0 errors, 0 warnings**
- Runtime: win-x64

## Files Changed
- `src/InControl.App/Controls/InputComposer.xaml`
- `src/InControl.App/Controls/InputComposer.xaml.cs`
- `src/InControl.App/Pages/ExtensionsPage.xaml`
