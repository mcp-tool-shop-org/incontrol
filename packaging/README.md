# Packaging

Nothing in this directory is an upload. Current main is app 2.0.0. Tag v0.3.0 is an older source-only release and is not this tree. Install the current tree from `docs/INSTALLATION.md`.

## Store identity

The Store listing is 9N1FG39JWF83, and its newest package is 1.4.0.0. The next package is accepted only when all of these match the Product Identity page in Partner Center, case-sensitive:

- Identity `Name` is `mcp-tool-shop.InControl-Desktop`. The 1.4.0 upload file was named `InControl.App_1.4.0_x64.msixupload`, but a file name is not the identity.
- Identity `Publisher` is `CN=5305D976-6952-4F00-9C21-3A5DB090359F`.
- `PublisherDisplayName` is `mcp-tool-shop`.
- `DisplayName` is a name reserved for this product. Today that is `InControl-Desktop`.
- Identity `Version` is four numbers, ends in `0`, and is higher than `1.4.0.0`. The prepared version is `2.0.0.0`.

Application `Id` stays `App`, as in 1.3.0, so Start and taskbar pins survive the update. The tile and Start menu say InControl. That is the `VisualElements` display name, which Partner Center does not check.

`TargetPlatformMinVersion` in `src/InControl.App/InControl.App.csproj` is `10.0.19041.0`, Windows 10 version 2004. Without it the build writes the SDK version, 22621, as `MinVersion`, and Windows 10 cannot install the package.

## Structure

```
packaging/
├── AppxManifest.template.xml    # Manifest for the signed workflow
├── Assets/                       # Unused. The package assets live in src/InControl.App/Assets
└── README.md                    # This file
```

`src/InControl.App/Package.appxmanifest` is the manifest a local MSBuild package uses. Keep its identity the same as the template.

## Icons

The tiles, taskbar icon, Store logo and splash screen are all drawn from `logo.png`: the llama mark, without the wordmark, on a transparent background. Regenerate every file in `src/InControl.App/Assets` at its current size with:

```powershell
py -3 scripts/generate-icons.py
```

## Version Substitution

The manifest template uses one placeholder:

| Placeholder | Replaced With |
|-------------|---------------|
| `${VERSION}` | Four-part package version. The prepared value is `2.0.0.0`. |

## Building Locally

`dotnet publish` on the .NET 9 SDK stops at MSB4062. That SDK does not ship `Microsoft.Build.AppxPackage.dll`, so `RemovePayloadDuplicates` cannot load. The package is produced by MSBuild from a Visual Studio install that includes the Windows App SDK packaging targets. Signing stays off. The Store re-signs the upload.

```powershell
msbuild src\InControl.App\InControl.App.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x64 /p:AppxPackageSigningEnabled=false /p:GenerateAppxPackageOnBuild=true /p:UapAppxPackageBuildMode=StoreUpload
```

The unsigned package and its `.msixupload` land under `AppPackages\`, which is gitignored. Check the manifest inside before uploading: `Name="mcp-tool-shop.InControl-Desktop"`, `Publisher="CN=5305D976-6952-4F00-9C21-3A5DB090359F"`, `PublisherDisplayName` `mcp-tool-shop`, `Version="2.0.0.0"`, and `MinVersion="10.0.19041.0"`.

KokoroSharp copies its voices and eSpeak data with an after-build step that never reaches the package. `InControl.App.csproj` adds that folder as package content. A package without `voices\` and `espeak\` at its root cannot speak.

## Signing

A tag does not build or sign an MSIX. `.github/workflows/release.yml` runs the library tests. `.github/workflows/release-signed.yml` is `workflow_dispatch` only, and it refuses to run until a signing certificate is configured. That is for a sideloaded package. Its certificate subject has to be `CN=5305D976-6952-4F00-9C21-3A5DB090359F`. Do not create a stand-in subject and upload the result.

The signed workflow writes `mcp-tool-shop.InControl-Desktop_<version>_x64.msix`. It rejects a package version below `2.0.0.0`, and it rejects a manifest whose identity name is not `mcp-tool-shop.InControl-Desktop`.

## Version Policy

The app version is `2.0.0` in `src/InControl.App/InControl.App.csproj`. The MSIX identity version is `2.0.0.0` in `src/InControl.App/Package.appxmanifest`. `AppxAutoIncrementPackageRevision` is off, so a rebuild does not move the fourth part. `InControl.Core` stays 1.2.2 and `InControl.Inference` stays 1.0.2.
