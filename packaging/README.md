# Packaging

Nothing in this directory is an upload. Current main is app 2.0.0. Tag v0.3.0 is an older source-only release and is not this tree. Install the current tree from `docs/INSTALLATION.md`.

Partner Center already has this product through package 1.4.0. The newest upload file is `InControl.App_1.4.0_x64.msixupload`. The next package is accepted only when both of these are true:

- Identity `Name` is exactly `InControl.App`. `InControl.Desktop` is a different package and is rejected.
- Identity `Version` is four numbers and higher than `1.4.0.0`. The prepared version is `2.0.0.0`.

The name on the repo, and the display name in the manifest, stay InControl. Display name is not the package identity.

`Publisher` is `CN=5305D976-6952-4F00-9C21-3A5DB090359F`. A package signed with a different subject is rejected even when the name and version are right. A local test certificate is not that publisher.

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
| Publisher | `CN=5305D976-6952-4F00-9C21-3A5DB090359F` |

## Building Locally

`dotnet publish` on the .NET 9 SDK stops at MSB4062. That SDK does not ship `Microsoft.Build.AppxPackage.dll`, so `RemovePayloadDuplicates` cannot load. The package is produced by MSBuild from a Visual Studio install that includes the Windows App SDK packaging targets. Signing stays off for a local build. The Store re-signs the upload.

```powershell
msbuild src\InControl.App\InControl.App.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x64 /p:AppxPackageSigningEnabled=false /p:GenerateAppxPackageOnBuild=true
```

The unsigned package lands under `AppPackages\`, which is gitignored. Pack `InControl.App_2.0.0.0_x64.msix` and its `.msixsym` at the root of a zip named `InControl.App_2.0.0.0_x64.msixupload`. Identity inside the manifest must already say `Name="InControl.App"`, `Publisher="CN=5305D976-6952-4F00-9C21-3A5DB090359F"`, and `Version="2.0.0.0"`.

## Signing

A tag does not build or sign an MSIX. `.github/workflows/release.yml` runs the library tests. `.github/workflows/release-signed.yml` is `workflow_dispatch` only, and it refuses to run until a signing certificate is configured. That certificate's subject has to be `CN=5305D976-6952-4F00-9C21-3A5DB090359F`. Do not create a stand-in subject and upload the result.

The signed workflow writes `InControl.App_<version>_x64.msix`. It rejects a package version below `2.0.0.0`, and it rejects a manifest whose identity name is not `InControl.App`.

## Version Policy

The app version is `2.0.0` in `src/InControl.App/InControl.App.csproj`. The MSIX identity version is `2.0.0.0` in `src/InControl.App/Package.appxmanifest`. `AppxAutoIncrementPackageRevision` is off, so a rebuild does not move the fourth part. `InControl.Core` stays 1.2.2 and `InControl.Inference` stays 1.0.2.
