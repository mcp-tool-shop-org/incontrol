# Phase 11 Commit 7 — Settings Completeness + Search + Grouping

## Summary
Implemented functional settings search that filters sections by keyword, added x:Name to all settings sections with search tags, wired up all remaining buttons, and added a "No Results" empty state for search.

## Changes Made

### SettingsPage.xaml

**Section Search Tags:**
Each section now has a `Tag` property with searchable keywords:
- **GeneralSection**: theme, light, dark, system, startup, launch, minimize, tray, background
- **ModelsSection**: model, default, gpu, cuda, directml, acceleration, context, length, tokens
- **AssistantSection**: assistant, memory, context, sessions, tool, extensions
- **MemorySection**: memory, storage, location, clear, history, data
- **ExtensionsSection**: extensions, plugins, tools, integrations, install
- **ConnectivitySection**: connectivity, network, offline, policy, connections, internet
- **UpdatesSection**: updates, automatic, version, check, download
- **DiagnosticsSection**: diagnostics, logs, export, reset, defaults, troubleshooting, support

**Named Buttons:**
- `ChangeStorageButton` - Memory storage location
- `ClearMemoryButton` - Clear all memory
- `CheckUpdatesButton` - Check for updates
- `ExportDiagnosticsButton` - Export diagnostics bundle
- `OpenLogsButton` - Open logs folder
- `ResetSettingsButton` - Reset to defaults

**No Results Panel:**
Added a panel that appears when search yields no matches:
- Search icon (E721)
- "No settings found for [query]" message
- "Try a different search term" hint

### SettingsPage.xaml.cs

**Search Implementation:**
- `CollectSections()` - Gathers all section StackPanels
- `FilterSettings(string searchText)` - Filters sections by Tag
- Case-insensitive search
- Shows all sections when search is empty
- Shows NoResultsPanel when no matches

**Button Handlers:**
- `OnChangeStorageClick` - Coming Soon dialog
- `OnClearMemoryClick` - Confirmation dialog
- `OnCheckUpdatesClick` - Version check dialog
- `OnExportDiagnosticsClick` - Coming Soon dialog
- `OnOpenLogsClick` - Opens logs folder in Explorer
- `OnResetSettingsClick` - Confirmation + theme reset

**Storage Path Display:**
- `InitializeStoragePath()` - Shows actual LocalApplicationData path

## Search UX

| Query | Visible Sections |
|-------|------------------|
| "theme" | General |
| "gpu" | Models |
| "memory" | Assistant, Memory |
| "update" | Updates |
| "log" | Diagnostics |
| "xyz" | No Results Panel |

## Build Verification
- Clean build: **0 errors, 0 warnings**
- Runtime: win-x64

## Files Changed
- `src/InControl.App/Pages/SettingsPage.xaml`
- `src/InControl.App/Pages/SettingsPage.xaml.cs`
