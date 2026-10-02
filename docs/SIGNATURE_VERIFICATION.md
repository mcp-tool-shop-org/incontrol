# Signature Verification Guide

## Why Verify?

Code signing provides cryptographic proof that:
1. The package came from InControl (not a malicious third party)
2. The package hasn't been modified since it was signed
3. The build can be traced to a specific commit and build process

## Quick Verification

### Windows GUI Method

1. Download the MSIX package
2. Right-click → **Properties**
3. Go to **Digital Signatures** tab
4. Select the signature and click **Details**
5. Verify:
   - "This digital signature is OK"
   - Signer name matches expected publisher

### Command Line Method

```powershell
# Using signtool (requires Windows SDK)
signtool verify /pa /v InControl-Desktop-x.y.z.msix
```

Expected output:
```
Successfully verified: InControl-Desktop-x.y.z.msix
```

## Checksum Verification

Each release includes a `checksums-x.y.z.txt` file with SHA256 hashes.

### PowerShell

```powershell
# Get the expected checksum
$checksumFile = "checksums-1.0.0.txt"
$expectedLine = Get-Content $checksumFile | Where-Object { $_ -match "InControl-Desktop" }
$expected = $expectedLine.Split(' ')[0]

# Calculate actual checksum
$actual = (Get-FileHash "InControl-Desktop-1.0.0.msix" -Algorithm SHA256).Hash.ToLower()

# Compare
if ($expected -eq $actual) {
    Write-Host "✅ Checksum verified - file is authentic" -ForegroundColor Green
} else {
    Write-Host "❌ Checksum mismatch - DO NOT INSTALL" -ForegroundColor Red
    Write-Host "Expected: $expected"
    Write-Host "Actual:   $actual"
}
```

### Linux/macOS

```bash
# Get expected checksum
expected=$(grep "InControl-Desktop" checksums-1.0.0.txt | cut -d' ' -f1)

# Calculate actual
actual=$(sha256sum InControl-Desktop-1.0.0.msix | cut -d' ' -f1)

# Compare
if [ "$expected" = "$actual" ]; then
    echo "✅ Checksum verified"
else
    echo "❌ Checksum mismatch"
fi
```

## Build Provenance

Each release includes provenance metadata showing:

| Field | Description |
|-------|-------------|
| version | Release version |
| build.number | CI build number |
| build.timestamp | When the build was created |
| source.commit | Full commit SHA |
| source.ref | Git ref (tag) |

### Verify Provenance

```powershell
# Extract provenance from package (if inspecting)
# Or download provenance.json from release assets

$provenance = Get-Content provenance.json | ConvertFrom-Json

# Verify commit matches
$tagCommit = git ls-remote origin refs/tags/v$provenance.version | ForEach-Object { $_.Split()[0] }
if ($tagCommit -eq $provenance.source.commit) {
    Write-Host "✅ Provenance matches tagged commit"
}
```

## Certificate Information

### Expected Publisher

Production releases are signed with:
- **Publisher:** `CN=InControl-Desktop` (or organization name if EV cert)
- **Algorithm:** SHA256
- **Timestamp:** RFC 3161 compliant (DigiCert)

### Certificate Chain

The signing certificate chains to a trusted root CA. Windows automatically validates this chain during installation.

## What To Do If Verification Fails

### Checksum Mismatch

1. **DO NOT INSTALL** the package
2. Re-download from the official source
3. Verify again
4. If still failing, report to security@example.com

### Signature Invalid

1. **DO NOT INSTALL** the package
2. Ensure you downloaded from official sources
3. Check if the certificate has been revoked
4. Report the issue

### SmartScreen Warning

If Windows SmartScreen shows a warning:

1. This is normal for new/uncommon applications
2. Verify the checksum independently
3. Click "More info" → "Run anyway" only if verification passed

For EV-signed releases, SmartScreen warnings should not appear.

## Reproducible Builds

While full reproducible builds are a future goal, you can verify:

1. **Source matches:** Compare commit hash in provenance to GitHub
2. **Build matches:** CI logs are public in GitHub Actions
3. **Package matches:** Checksums are published with releases

## Security Reporting

If you discover a security issue with our signing or distribution:

1. **DO NOT** post publicly
2. Email security@example.com with:
   - Description of the issue
   - Steps to reproduce
   - Any relevant files or hashes
3. We will respond within 48 hours

## FAQ

### Why is the package unsigned on some platforms?

Unsigned packages are only produced for:
- Development builds (never released)
- CI testing (not distributed)

All official releases are signed.

### Can I verify the signature offline?

The initial signature verification requires network access to check the certificate revocation list (CRL). After first verification, Windows caches the result.

### How do I know the checksum file itself is authentic?

The checksum file is:
1. Published alongside the release on GitHub
2. Part of the signed GitHub release (GitHub's infrastructure)
3. Can be compared across multiple download sources
