# Installation Guide

## System Requirements

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| Operating System | Windows 10 (1809+) | Windows 11 |
| Architecture | x64 | x64 |
| RAM | 4 GB | 8 GB |
| Disk Space | 500 MB | 1 GB |
| GPU (for ML) | None | NVIDIA RTX (CUDA 12+) |

## Distribution Channels

### Primary: GitHub Releases (Recommended)

Official releases are published at:
```
https://github.com/mcp-tool-shop-org/InControl-Desktop/releases
```

**Why GitHub Releases?**
- Cryptographically signed packages
- Verifiable checksums
- Full release notes and changelogs
- Previous versions always available
- No account required to download

### Alternative: Direct Download

For environments without GitHub access:
```
https://incontrol.example.com/download/latest
```

## Installation Steps

### Step 1: Download

1. Go to [GitHub Releases](https://github.com/mcp-tool-shop-org/InControl-Desktop/releases)
2. Download `InControl-Desktop-x.y.z.msix`
3. Download `checksums-x.y.z.txt`

### Step 2: Verify (Recommended)

**Verify checksum:**
```powershell
# Get expected checksum
$expected = (Get-Content checksums-x.y.z.txt | Select-String 'InControl-Desktop').ToString().Split(' ')[0]

# Calculate actual checksum
$actual = (Get-FileHash InControl-Desktop-x.y.z.msix -Algorithm SHA256).Hash.ToLower()

# Compare
if ($expected -eq $actual) {
    Write-Host "✅ Checksum verified" -ForegroundColor Green
} else {
    Write-Host "❌ DO NOT INSTALL - Checksum mismatch" -ForegroundColor Red
}
```

**Verify signature:**
1. Right-click the MSIX file → Properties
2. Go to Digital Signatures tab
3. Verify the signature shows "OK"
4. Verify the publisher name matches

### Step 3: Install

1. Double-click `InControl-Desktop-x.y.z.msix`
2. Windows will show the app installer dialog
3. Review the permissions requested
4. Click "Install"

**What you'll see:**
- Publisher name (should match expected)
- Permissions requested (local file access only)
- Install location (cannot be changed for MSIX)

### Step 4: First Run

1. Launch InControl from Start Menu
2. Complete the onboarding wizard
3. Configure your preferences

## Upgrade Path

### From Previous Version

1. Download the new MSIX package
2. Verify as described above
3. Double-click to install (previous version is automatically replaced)
4. Your settings and data are preserved

**What's preserved:**
- User settings
- Memory items
- Assistant preferences
- Update mode selection

**What's not preserved (by design):**
- Temporary files
- Cache data

### From Pre-release to Stable

Pre-release versions (alpha, beta, rc) can upgrade directly to stable.

## Uninstallation

### Standard Uninstall

1. Open Windows Settings → Apps → Installed apps
2. Find "InControl Desktop"
3. Click the menu (⋯) → Uninstall
4. Confirm

### What Gets Removed

| Item | Removed | Notes |
|------|---------|-------|
| Application files | ✅ Yes | Completely removed |
| Settings | ✅ Yes | App-specific settings |
| User data | ❌ No | Documents folder preserved |
| Memory items | ⚠️ Optional | See Data Cleanup below |

### Data Cleanup (Optional)

User data is stored at:
```
%LOCALAPPDATA%\InControl\
```

To completely remove all data:
```powershell
Remove-Item -Path "$env:LOCALAPPDATA\InControl" -Recurse -Force
```

**Warning:** This permanently deletes:
- Memory items
- Conversation history
- Custom configurations

## Troubleshooting

### "Windows protected your PC" (SmartScreen)

This appears for unsigned or new applications.

**For signed releases:**
1. Click "More info"
2. Verify the publisher name matches
3. Click "Run anyway"

**For unsigned releases (development only):**
1. Do not install unsigned releases in production
2. For testing, click "More info" → "Run anyway"

### "App package is already installed"

The same version is already installed.

**Solutions:**
1. Uninstall first, then reinstall
2. Or download a different version

### "This app package is not supported"

Architecture mismatch (trying to install x64 on ARM).

**Solution:**
Download the correct architecture package.

### Installation hangs

**Try:**
1. Cancel and retry
2. Restart Windows
3. Run as Administrator

### "Publisher could not be verified"

The package is unsigned or certificate is not trusted.

**For official releases:**
- This should not happen
- Report as a security issue

**For development builds:**
- Expected behavior
- Only install if you built it yourself

## Silent Installation (Enterprise)

For automated deployment:

```powershell
# Install
Add-AppxPackage -Path "InControl-Desktop-x.y.z.msix"

# Uninstall
Get-AppxPackage -Name "InControl.Desktop" | Remove-AppxPackage
```

**Note:** MSIX packages require no admin rights for per-user install.

## FAQ

### Where is the application installed?

MSIX packages are installed to:
```
C:\Program Files\WindowsApps\InControl.Desktop_x.y.z.w_x64__...
```

This location is managed by Windows and cannot be changed.

### Can I install without Microsoft Store?

Yes. The MSIX package can be installed directly (sideloading) without the Microsoft Store. Ensure you have sideloading enabled in Windows Settings → Developer Settings.

### Do I need admin rights?

No. MSIX per-user installation does not require admin rights.

### How do I install on multiple machines?

Download and install on each machine, or use enterprise deployment tools (SCCM, Intune, Group Policy).

### Can I have multiple versions installed?

No. MSIX packages replace previous versions. For testing multiple versions, use virtual machines.
