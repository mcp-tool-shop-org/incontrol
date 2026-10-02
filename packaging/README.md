# Packaging

Nothing in this directory is an upload. Tag v0.3.0 is the source tree. Install that release from `docs/INSTALLATION.md`.

Partner Center already has this product through package 1.4.0. The newest upload file is `InControl.App_1.4.0_x64.msixupload`. The next package is accepted only when both of these are true:

- Identity `Name` is exactly `InControl.App`. `InControl.Desktop` is a different package and is rejected.
- Identity `Version` is four numbers and higher than `1.4.0.0`. The prepared version is `2.0.0.0`.

The name on the repo, and the display name in the manifest, stay InControl. Display name is not the package identity.

`Publisher` is the certificate subject on the 1.4.0 upload. That subject is not in this repository. Leave `${PUBLISHER}` until it is known. A local test certificate is not that publisher, and a package signed with a different subject is rejected even when the name and version are right.

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
| `${VERSION}` | Four-part package version. The prepared value is `2.0.0.0`. |
| `${PUBLISHER}` | Certificate subject from the 1.4.0 upload. Do not guess it. |

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
# Identity inside the manifest must already say Name="InControl.App" and Version="2.0.0.0".
makeappx pack /d ./publish /p InControl.App_2.0.0.0_x64.msix /nv
```

## Signing

A tag does not build or sign an MSIX. `.github/workflows/release.yml` runs the library tests. `.github/workflows/release-signed.yml` is `workflow_dispatch` only, and it refuses to run until a signing certificate is configured. That certificate's subject has to be the publisher already on the 1.4.0 package. Do not create a stand-in subject and upload the result.

The signed workflow writes `InControl.App_<version>_x64.msix`. It rejects a package version below `2.0.0.0`, and it rejects a manifest whose identity name is not `InControl.App`.

## Version Policy

The app version is `2.0.0` in `src/InControl.App/InControl.App.csproj`. The MSIX identity version is `2.0.0.0` in `src/InControl.App/Package.appxmanifest`. `AppxAutoIncrementPackageRevision` is off, so a rebuild does not move the fourth part. `InControl.Core` stays 1.2.2 and `InControl.Inference` stays 1.0.2.
