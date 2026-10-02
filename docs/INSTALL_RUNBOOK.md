# InControl Installation Runbook

> Clean machine installation, upgrade, and uninstall procedures.

## Prerequisites

### System Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| OS | Windows 10 22H2 | Windows 11 23H2+ |
| RAM | 8 GB | 16 GB |
| Storage | 500 MB (app) + model space | SSD preferred |
| GPU | Any (CPU fallback) | NVIDIA RTX (for Ollama GPU) |

### Required Dependencies

| Dependency | Required | Notes |
|------------|----------|-------|
| .NET 9.0 Runtime | Bundled | Self-contained deployment |
| VC++ Runtime | System | Usually pre-installed |
| Windows App SDK | Bundled | Included in MSIX |
| Ollama | Optional | Required for model inference |

## Fresh Install Procedure

### Step 1: Download

1. Go to [Releases](https://github.com/mcp-tool-shop-org/InControl-Desktop/releases)
2. Download `InControl.App_X.Y.Z_x64.msix`
3. Download `checksums-X.Y.Z.txt`

### Step 2: Verify Integrity

```powershell
# Verify checksum
$expected = Get-Content checksums-X.Y.Z.txt | Select-String "InControl.App"
$actual = (Get-FileHash InControl.App_X.Y.Z_x64.msix -Algorithm SHA256).Hash
if ($actual -eq $expected.ToString().Split()[0]) {
    Write-Host "Checksum verified!" -ForegroundColor Green
} else {
    Write-Host "CHECKSUM MISMATCH - Do not install!" -ForegroundColor Red
}
```

### Step 3: Install

**Option A: Double-click**
- Double-click the `.msix` file
- Click "Install" in the App Installer dialog

**Option B: PowerShell**
```powershell
Add-AppPackage -Path "InControl.App_X.Y.Z_x64.msix"
```

### Step 4: First Run

1. Launch from Start Menu → "InControl"
2. **Quick Start Panel** appears:
   - Step 1: Choose a model (opens Model Manager)
   - Step 2: Create a session
   - Step 3: Try a prompt
3. If Ollama is running, models appear automatically
4. If Ollama is not running, see "Ollama not running" message

### Step 5: Verify Installation

```powershell
# Check installed package
Get-AppPackage -Name "MCPToolShop.InControl"

# Check app data location
Test-Path "$env:LOCALAPPDATA\InControl"
```

## First Run Checklist

- [ ] App launches without error
- [ ] Quick Start panel visible
- [ ] Model Manager opens (gear icon or Step 1)
- [ ] Ollama connection status shown
- [ ] Theme matches system (Dark/Light)
- [ ] Status strip shows at bottom

## Add First Model

1. Click "Model Manager" or Step 1 in Quick Start
2. If Ollama connected:
   - See "Ollama Models" header with llama icon
   - Click "Pull Model" or use quick-pull buttons
   - Popular options: llama3.2, mistral, codegemma
3. If Ollama not connected:
   - Install Ollama from https://ollama.com
   - Start Ollama service
   - Click "Refresh" in Model Manager

## Run First Prompt

1. Select a model from Model Manager (or it auto-selects)
2. Return to main screen (Back button or Escape)
3. Type a prompt in the input area at bottom
4. Press Ctrl+Enter or click Send
5. Watch streaming response appear

## Upgrade Procedure

### Before Upgrade

1. **Export settings** (if critical customizations exist)
2. Note current version: Help → About
3. Close InControl completely

### Upgrade Steps

**Option A: MSIX Auto-Update**
- App Installer handles updates automatically
- User data persists in `%LOCALAPPDATA%\InControl`

**Option B: Manual Upgrade**
```powershell
# Install new version (upgrades in place)
Add-AppPackage -Path "InControl.App_X.Y.Z_x64.msix"
```

### After Upgrade

1. Launch InControl
2. Verify new version in Help → About
3. Check that:
   - [ ] Sessions still visible in sidebar
   - [ ] Models still listed in Model Manager
   - [ ] Settings preserved
   - [ ] Theme unchanged

### Data Persistence Rules

| Data Type | Location | Persists on Upgrade | Removed on Uninstall |
|-----------|----------|---------------------|----------------------|
| Sessions | `%LOCALAPPDATA%\InControl\sessions` | ✅ Yes | ❌ No (manual) |
| Settings | `%LOCALAPPDATA%\InControl\settings.json` | ✅ Yes | ❌ No (manual) |
| Logs | `%LOCALAPPDATA%\InControl\Logs` | ✅ Yes | ❌ No (manual) |
| Models | Ollama directory | ✅ Yes (Ollama-managed) | N/A |
| Cache | `%LOCALAPPDATA%\InControl\cache` | ✅ Yes | ❌ No (manual) |

## Uninstall Procedure

### Standard Uninstall

**Option A: Settings App**
1. Windows Settings → Apps → Installed apps
2. Search "InControl"
3. Click ⋮ → Uninstall
4. Confirm

**Option B: PowerShell**
```powershell
Get-AppPackage -Name "MCPToolShop.InControl" | Remove-AppPackage
```

### What Gets Removed

| Removed | Preserved |
|---------|-----------|
| Application files | User data in `%LOCALAPPDATA%\InControl` |
| Start menu shortcut | Ollama models |
| App registration | System logs |

### Complete Cleanup (Optional)

To remove ALL data including sessions and settings:

```powershell
# Remove app
Get-AppPackage -Name "MCPToolShop.InControl" | Remove-AppPackage

# Remove user data (DESTRUCTIVE - backs up first)
$dataPath = "$env:LOCALAPPDATA\InControl"
if (Test-Path $dataPath) {
    $backup = "$env:USERPROFILE\Desktop\InControl-Backup-$(Get-Date -Format 'yyyyMMdd')"
    Copy-Item -Path $dataPath -Destination $backup -Recurse
    Remove-Item -Path $dataPath -Recurse -Force
    Write-Host "Data backed up to: $backup"
    Write-Host "Data removed from: $dataPath"
}
```

## Troubleshooting

### Install Fails

| Error | Cause | Solution |
|-------|-------|----------|
| "Publisher unknown" | Unsigned package | Enable sideloading in Settings |
| "Dependencies missing" | SDK issue | Install Windows App SDK |
| "Already installed" | Version conflict | Uninstall first, then reinstall |

### First Run Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Blank window | XAML resource error | Check Windows version compatibility |
| Crash on Model Manager | Missing theme resources | Reinstall, check logs |
| Ollama not detected | Service not running | Start Ollama, click Refresh |

### Upgrade Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Settings lost | Data migration bug | Restore from backup |
| Version unchanged | Cache issue | Restart, verify package version |

## Support

- **Logs**: `%LOCALAPPDATA%\InControl\Logs`
- **Support Bundle**: Settings → Export Support Bundle
- **Issues**: https://github.com/mcp-tool-shop-org/InControl-Desktop/issues
