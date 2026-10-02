# Phase 11 Commit 3 — Navigation + Backstack Consistency

## Summary
Implemented a proper NavigationService with backstack support, ensuring consistent navigation behavior across all pages. Back button now returns to the previous page instead of always going home.

## Problem Solved
Previously:
- All Back buttons went directly to Home
- Navigation from Settings → ModelManager → Back would go to Home, not Settings
- No support for Alt+Left (standard Windows back navigation)
- Escape always went to Home instead of back through history

## Changes Made

### New Files
- `src/InControl.App/Services/NavigationService.cs` - Navigation service with backstack
  - Singleton pattern for app-wide navigation state
  - `Navigate<T>()` - Navigate to a page with optional backstack
  - `GoBack()` - Return to previous page
  - `GoHome()` - Return to home, clearing backstack
  - `CanGoBack` property for UI state
  - Events: `Navigated`, `NavigatedBack`, `NavigatedHome`

### MainWindow.xaml.cs
- Integrated NavigationService for all navigation operations
- Added `SetupNavigation()` to subscribe to navigation events
- Changed all `NavigateToPage<T>()` calls to use `_navigation.Navigate<T>()`
- Changed all `NavigateHome()` calls to use `_navigation.GoBack()` or `_navigation.GoHome()`
- Added `OnNavigated` and `OnNavigatedBack` handlers
- Refactored page instantiation to `NavigateToPageInternal(Type)`
- Extracted `WirePageEvents()` for cleaner code organization
- Added Alt+Left keyboard shortcut for back navigation

## Navigation Flow Examples

### Before (Broken)
```
Home → Settings → ModelManager → [Back] → Home (wrong!)
```

### After (Correct)
```
Home → Settings → ModelManager → [Back] → Settings → [Back] → Home
```

## Keyboard Shortcuts
| Shortcut | Action |
|----------|--------|
| Escape | Go back (or close overlay) |
| Alt+Left | Go back (standard Windows) |

## Navigation Behavior
| Action | Adds to Backstack? |
|--------|-------------------|
| AppBar button click | Yes |
| StatusStrip item click | Yes |
| CommandPalette action | Yes |
| Page sub-navigation (e.g., Settings→ModelManager) | Yes |
| Back button | No (pops from backstack) |
| Escape key | No (pops from backstack) |
| Home/New Session | No (clears backstack) |

## Build Verification
- Clean build: **0 errors, 0 warnings**
- Runtime: win-x64

## Files Changed
- `src/InControl.App/Services/NavigationService.cs` (new)
- `src/InControl.App/MainWindow.xaml.cs` (major refactor)
