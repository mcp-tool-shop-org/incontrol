# Packaging

This directory contains resources for packaging InControl-Desktop as an MSIX.

## Structure

```
packaging/
├── AppxManifest.template.xml    # MSIX manifest template
├── Assets/                       # Package assets (icons, splash)
│   └── .gitkeep                 # Placeholder
└── README.md                    # This file
```

## Required Assets

Before first release, add the following PNG files to `Assets/`:

| File | Size | Purpose |
|------|------|---------|
| StoreLogo.png | 50x50 | Store listing |
| Square44x44Logo.png | 44x44 | Taskbar icon |
| Square150x150Logo.png | 150x150 | Start menu tile |
| Wide310x150Logo.png | 310x150 | Wide tile |
| SplashScreen.png | 620x300 | App loading |

## Version Substitution

The manifest template uses these placeholders:

| Placeholder | Replaced With |
|-------------|---------------|
| `${VERSION}` | MSIX version (x.y.z.w format) |
| `${PUBLISHER}` | Certificate publisher CN |

## Building Locally

To test packaging locally:

```powershell
# Publish the app
dotnet publish src/InControl.App/InControl.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output ./publish

# Copy manifest (replace placeholders manually or use script)
Copy-Item packaging/AppxManifest.template.xml ./publish/AppxManifest.xml
# Edit AppxManifest.xml to replace ${VERSION} and ${PUBLISHER}

# Package (requires Windows SDK)
makeappx pack /d ./publish /p InControl-Desktop.msix /nv
```

## Signing

Signing is handled by the CI pipeline. For local testing:

```powershell
# Create test certificate (one-time)
New-SelfSignedCertificate `
  -Type Custom `
  -Subject "CN=InControl-Desktop-Dev" `
  -KeyUsage DigitalSignature `
  -FriendlyName "InControl Dev Cert" `
  -CertStoreLocation "Cert:\CurrentUser\My"

# Sign package
signtool sign /fd SHA256 /a /f cert.pfx /p password InControl-Desktop.msix
```

## CI Pipeline

The release workflow (`../.github/workflows/release.yml`):

1. Builds the application
2. Runs all tests
3. Publishes self-contained executable
4. Generates build metadata
5. Creates MSIX package
6. Generates SHA256 checksums
7. Creates GitHub release (draft)

## Version Policy

Version is sourced from the project file:
- `src/InControl.App/InControl.App.csproj`

CI reads and injects this version. Manual version management is forbidden.
