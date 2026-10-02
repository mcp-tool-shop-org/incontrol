# Phase 11 Commit 2 — Dark/Light Mode Toggle (First-Class)

## Summary
Added first-class theme switching capability with a dedicated toggle button in the AppBar and a functional theme selector in Settings.

## Changes Made

### New Files
- `src/InControl.App/Services/ThemeService.cs` - Singleton service for managing app theme
  - Supports Light, Dark, and System themes
  - Provides toggle and cycle functionality
  - Notifies subscribers on theme changes
  - Returns appropriate icons for each theme state

### AppBar.xaml
- Added ThemeToggleButton before Settings button
- Added ThemeIcon FontIcon for dynamic icon updates

### AppBar.xaml.cs
- Added `ThemeToggleButton.Click` handler → toggles between Light/Dark
- Added `OnThemeChanged` subscription → updates icon when theme changes
- Added `UpdateThemeIcon()` → sets appropriate glyph and tooltip

### SettingsPage.xaml
- Added `x:Name="ThemeComboBox"` to theme selection dropdown

### SettingsPage.xaml.cs
- Added `InitializeThemeComboBox()` → sets initial selection from current theme
- Added `OnThemeSelectionChanged` → updates ThemeService when user selects theme
- Added `ThemeChanged` event for external notification

### MainWindow.xaml.cs
- Added `InitializeTheme()` → initializes ThemeService with root element
- Theme service initialization happens before other setup

## Theme Icons
| Theme | Icon | Glyph |
|-------|------|-------|
| Light | Sun | \uE706 |
| Dark | Moon | \uE708 |
| System | Settings | \uE770 |

## User Interaction Flow

### Quick Toggle (AppBar)
1. Click sun/moon icon in AppBar
2. Theme toggles: Light ↔ Dark
3. Icon updates to reflect new theme
4. Tooltip shows current theme

### Detailed Selection (Settings)
1. Open Settings page
2. Find Theme in General section
3. Select Light, Dark, or System
4. Theme applies immediately
5. Selection persists

## Build Verification
- Clean build: **0 errors, 0 warnings**
- Runtime: win-x64

## Files Changed
- `src/InControl.App/Services/ThemeService.cs` (new)
- `src/InControl.App/Controls/AppBar.xaml`
- `src/InControl.App/Controls/AppBar.xaml.cs`
- `src/InControl.App/Pages/SettingsPage.xaml`
- `src/InControl.App/Pages/SettingsPage.xaml.cs`
- `src/InControl.App/MainWindow.xaml.cs`
