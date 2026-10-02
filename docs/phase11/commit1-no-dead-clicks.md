# Phase 11 Commit 1 — No Dead Clicks Sweep + Click Feedback

## Summary
Comprehensive audit and fix of all clickable UI elements to ensure every button, menu item, and interactive control has proper event handling.

## Changes Made

### SessionSidebar.xaml.cs
- Added `SetupEventHandlers()` method
- Wired `NewSessionButton.Click` event → raises `NewSessionRequested`
- Wired `SessionSearch.TextChanged` and `QuerySubmitted` events → raises `SearchTextChanged`
- Wired all context menu items with "Coming Soon" dialogs:
  - `RenameMenuItem.Click`
  - `DuplicateMenuItem.Click`
  - `PinMenuItem.Click`
  - `ExportMenuItem.Click`
  - `DeleteMenuItem.Click`
- Wired `SessionList.ItemClick` and `PinnedList.ItemClick` → raises `SessionSelected`
- Added `SessionItem` class for typed session list items
- Added public events: `NewSessionRequested`, `SessionSelected`, `SearchTextChanged`

### StatusStrip.xaml.cs
- Added `MemoryClicked` event
- Wired `MemoryButton.Click` → raises `MemoryClicked`

### MainWindow.xaml.cs
- Added `StatusStrip.MemoryClicked` handler → navigates to Settings
- Added `SessionSidebar.NewSessionRequested` handler → navigates home for new session

### InputComposer.xaml
- Added `x:Name` to previously unnamed menu items:
  - `AddPreviousOutputMenuItem`
  - `AddFileMenuItem`

### InputComposer.xaml.cs
- Wired `AddPreviousOutputMenuItem.Click` → shows "Coming Soon" dialog
- Wired `AddFileMenuItem.Click` → raises existing `AttachFileRequested`
- Wired `ClearContextMenuItem.Click` → clears context count

### MessageCard.xaml.cs
- Added `SetupEventHandlers()` method
- Wired `CopyMenuItem.Click` → copies message to clipboard
- Wired `CopyAsMarkdownMenuItem.Click` → copies message to clipboard (as-is)
- Wired `AddToContextMenuItem.Click` → shows "Coming Soon" dialog

## Button Inventory Results

| Control | Total Buttons | Wired | Dead Before | Dead After |
|---------|---------------|-------|-------------|------------|
| AppBar | 9 | 9 | 0 | 0 |
| StatusStrip | 6 | 6 | 1 (MemoryButton) | 0 |
| InputComposer | 7 (incl menu) | 7 | 2 | 0 |
| SessionSidebar | 8 (incl menu) | 8 | 8 | 0 |
| MessageCard | 3 (menu) | 3 | 3 | 0 |
| WelcomePanel | 5 | 5 | 0 | 0 |
| CommandPalette | 1 | 1 | 0 | 0 |
| All Pages | ~25 | ~25 | 0 | 0 |

**Total dead clicks fixed: 14**

## Pattern Used
For unimplemented features, consistent "Coming Soon" dialog pattern:
```csharp
private async void OnFeatureClick(object sender, RoutedEventArgs e)
{
    var dialog = new ContentDialog
    {
        Title = "Coming Soon",
        Content = $"{feature} will be available in a future update.",
        CloseButtonText = "OK",
        XamlRoot = this.XamlRoot
    };
    await dialog.ShowAsync();
}
```

## Build Verification
- Clean build: **0 errors, 0 warnings**
- Runtime: win-x64
- All new event handlers compile and wire correctly

## Files Changed
- `src/InControl.App/Controls/SessionSidebar.xaml.cs`
- `src/InControl.App/Controls/StatusStrip.xaml.cs`
- `src/InControl.App/Controls/InputComposer.xaml`
- `src/InControl.App/Controls/InputComposer.xaml.cs`
- `src/InControl.App/Controls/MessageCard.xaml.cs`
- `src/InControl.App/MainWindow.xaml.cs`
