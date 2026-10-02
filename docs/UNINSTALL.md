# Uninstallation Guide

## Overview

InControl-Desktop is designed to uninstall cleanly, respecting your data and leaving no surprises.

## What Happens During Uninstall

### Removed Automatically

| Item | Location | Notes |
|------|----------|-------|
| Application | WindowsApps | Completely removed |
| Shortcuts | Start Menu | Removed |
| File associations | Registry | Cleaned up |
| App settings | App package data | Removed |

### Preserved (Not Touched)

| Item | Location | Why |
|------|----------|-----|
| User documents | Documents folder | Your data, your choice |
| Memory items | LocalAppData\InControl | Optional cleanup |
| Logs | LocalAppData\InControl\logs | Optional cleanup |

## Uninstall Methods

### Method 1: Windows Settings (Recommended)

1. Open **Settings** (Win + I)
2. Go to **Apps** → **Installed apps**
3. Search for "InControl"
4. Click the menu (⋯) → **Uninstall**
5. Confirm when prompted

### Method 2: Start Menu

1. Open **Start Menu**
2. Find "InControl"
3. Right-click → **Uninstall**
4. Confirm when prompted

### Method 3: PowerShell

```powershell
# Find the package
Get-AppxPackage -Name "InControl.Desktop"

# Uninstall
Get-AppxPackage -Name "InControl.Desktop" | Remove-AppxPackage
```

### Method 4: Control Panel (Classic)

1. Open **Control Panel**
2. Go to **Programs** → **Programs and Features**
3. Find "InControl Desktop"
4. Click **Uninstall**

## Data Cleanup Options

### Option 1: Keep User Data (Default)

Uninstalling keeps your data for potential reinstallation.

Data location:
```
%LOCALAPPDATA%\InControl\
```

Contains:
- Memory items
- Conversation history
- User preferences
- Logs

### Option 2: Complete Removal

To remove all InControl data:

```powershell
# Uninstall the application first
Get-AppxPackage -Name "InControl.Desktop" | Remove-AppxPackage

# Remove all user data
$dataPath = "$env:LOCALAPPDATA\InControl"
if (Test-Path $dataPath) {
    Remove-Item -Path $dataPath -Recurse -Force
    Write-Host "✅ Data removed: $dataPath"
} else {
    Write-Host "No data found at: $dataPath"
}
```

**Warning:** This permanently deletes:
- All memory items
- Conversation history
- Custom configurations
- Logs and diagnostics

### Option 3: Selective Cleanup

Keep some data, remove others:

```powershell
$basePath = "$env:LOCALAPPDATA\InControl"

# Remove only logs
Remove-Item -Path "$basePath\logs" -Recurse -Force

# Remove only cache
Remove-Item -Path "$basePath\cache" -Recurse -Force

# Keep memory and settings
Write-Host "Memory and settings preserved"
```

## Verifying Complete Removal

After uninstall, verify:

### Check Application

```powershell
# Should return nothing
Get-AppxPackage -Name "InControl.Desktop"
```

### Check Data (if cleaned)

```powershell
# Should return False
Test-Path "$env:LOCALAPPDATA\InControl"
```

### Check Start Menu

1. Open Start Menu
2. Search for "InControl"
3. Should show no results

## Reinstallation After Uninstall

### Fresh Install

If you removed all data:
1. Download latest MSIX
2. Install normally
3. Complete onboarding

### Reinstall with Data

If you kept user data:
1. Download latest MSIX
2. Install normally
3. Your settings and memory are restored

## Troubleshooting

### "Uninstall failed"

**Try:**
1. Close InControl if running
2. Restart Windows
3. Try uninstall again

### "Cannot find the package"

The package may already be uninstalled or was a development build.

```powershell
# List all InControl-related packages
Get-AppxPackage | Where-Object { $_.Name -like "*InControl*" }
```

### "Access denied"

**Try:**
1. Close any file explorer windows in InControl folders
2. End any InControl processes in Task Manager
3. Try again

### "Uninstall stuck"

If uninstall hangs:

```powershell
# Force remove (use with caution)
Get-AppxPackage -Name "InControl.Desktop" | Remove-AppxPackage -ForceTargetApplicationShutdown
```

## Enterprise Uninstall

For IT administrators:

### PowerShell Script

```powershell
# Uninstall for all users
Get-AppxPackage -AllUsers -Name "InControl.Desktop" | Remove-AppxPackage -AllUsers

# Remove user data (run as each user or via login script)
Remove-Item -Path "$env:LOCALAPPDATA\InControl" -Recurse -Force -ErrorAction SilentlyContinue
```

### Intune/SCCM

Use the following uninstall command:
```
powershell.exe -ExecutionPolicy Bypass -Command "Get-AppxPackage -Name 'InControl.Desktop' | Remove-AppxPackage"
```

## Data Export Before Uninstall

If you want to preserve your data:

1. Open InControl
2. Go to Settings → Data → Export
3. Choose export location
4. Uninstall
5. After reinstall, import your data

## Feedback

If you're uninstalling due to issues:
- Please consider filing a bug report
- Your feedback helps improve the application
- No personal data is included in bug reports

## FAQ

### Will uninstalling delete my files?

No. Files you created in Documents are never touched.

### What if I reinstall later?

If you kept user data, your settings and memory will be restored.

### Can I recover deleted data?

No. Once you run the data cleanup commands, data is permanently deleted.

### Does uninstall affect other applications?

No. InControl is sandboxed and does not affect other applications.
