# Uninstallation

Version 0.3.0 is a source build. It is not an installed MSIX, so it will not appear as an InControl-Desktop package in Windows Settings, and `Get-AppxPackage` will not remove it.

## Remove the app

1. Close InControl if `dotnet run` is still going.
2. Delete the clone you built.

## Local data

A local run stores settings and logs under `%LOCALAPPDATA%\InControl\`. Deleting the clone leaves that folder in place. To remove it:

```powershell
Remove-Item -Path "$env:LOCALAPPDATA\InControl" -Recurse -Force
```

That deletes conversation history kept on this PC. It does not delete anything on a GPU rental you connected.

## Reinstall

Clone the repository again and build from source. See `docs/INSTALLATION.md`. Do not download an MSIX.
