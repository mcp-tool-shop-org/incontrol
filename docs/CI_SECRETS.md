# CI Secrets Management

## Overview

This document describes how secrets are managed in the CI/CD pipeline for InControl.

## Required Secrets

| Secret Name | Purpose | Required For |
|-------------|---------|--------------|
| `SIGNING_CERTIFICATE_BASE64` | Code signing certificate (PFX, base64 encoded) | Signed releases |
| `SIGNING_CERTIFICATE_PASSWORD` | Password for the PFX file | Signed releases |

## Setting Up Secrets

The package publisher is `CN=5305D976-6952-4F00-9C21-3A5DB090359F`. A certificate with any other subject is rejected. The Store re-signs an upload, so a local test certificate is still not uploadable. See `packaging/README.md`.

`SIGNING_CERTIFICATE_BASE64` is an upload credential only when the certificate subject is that publisher.

A local test certificate is not the publisher and is not uploadable. Do not store one as the signing secret for a Partner Center upload, and do not mint a stand-in subject for that upload.

### Encode a known certificate for GitHub

Only for a certificate whose subject is `CN=5305D976-6952-4F00-9C21-3A5DB090359F`:

```powershell
# Convert PFX to base64
$pfxPath = "path/to/signing-cert.pfx"
$base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($pfxPath))

# Copy to clipboard
$base64 | Set-Clipboard
Write-Host "Base64 certificate copied to clipboard"

# Or save to file (delete after use!)
$base64 | Out-File -FilePath "cert-base64.txt" -Encoding utf8
```

### Add to GitHub Secrets

1. Go to repository → Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Add `SIGNING_CERTIFICATE_BASE64` with the base64 content
4. Add `SIGNING_CERTIFICATE_PASSWORD` with the PFX password

## Security Best Practices

### Certificate Storage

- **Never** commit certificates to the repository
- **Never** share certificate passwords in plain text
- Use GitHub's encrypted secrets exclusively
- Rotate certificates before expiration

### Access Control

- Limit who can access repository secrets
- Use environment protection rules for production secrets
- Audit secret access periodically

### Certificate Lifecycle

| Action | When |
|--------|------|
| Review | Quarterly |
| Rotate | Before expiration |
| Revoke | If compromised |

## Workflow Integration

### How Secrets Are Used

```yaml
# In release-signed.yml
- name: Setup Signing Certificate
  env:
    SIGNING_CERTIFICATE_BASE64: ${{ secrets.SIGNING_CERTIFICATE_BASE64 }}
    SIGNING_CERTIFICATE_PASSWORD: ${{ secrets.SIGNING_CERTIFICATE_PASSWORD }}
  run: |
    # Decode and save temporarily
    $certBytes = [Convert]::FromBase64String($env:SIGNING_CERTIFICATE_BASE64)
    $certPath = "$env:RUNNER_TEMP\signing-cert.pfx"
    [IO.File]::WriteAllBytes($certPath, $certBytes)
```

### Cleanup

The workflow always cleans up the certificate file:

```yaml
- name: Cleanup Certificate
  if: always()
  run: |
    if (Test-Path $env:CERT_PATH) {
      Remove-Item -Path $env:CERT_PATH -Force
    }
```

## Troubleshooting

### "Signing certificate not configured"

The secret is not set or is empty:
1. Verify the secret exists in GitHub
2. Check the secret name matches exactly
3. Ensure the base64 encoding is correct

### "Invalid PFX password"

The password doesn't match:
1. Verify the password secret
2. Re-export the PFX with a known password
3. Update both secrets

### "Certificate not valid for code signing"

The certificate lacks code signing EKU:
1. Check certificate properties
2. Ensure it has "Code Signing" in Enhanced Key Usage
3. A replacement still has to be `CN=5305D976-6952-4F00-9C21-3A5DB090359F`. A different subject is not uploadable.

## Environment-Specific Certificates

Do not use a different subject for a Partner Center upload. A local test certificate is not that publisher and is not uploadable.

| Environment | Certificate | Purpose |
|-------------|-------------|---------|
| Local test | A test certificate | Not the publisher. Not uploadable. |
| Partner Center | `CN=5305D976-6952-4F00-9C21-3A5DB090359F` | The only uploadable subject |

Configure using GitHub environments:
1. Create environments (staging, production)
2. Add environment-specific secrets
3. Update workflow to use environment

```yaml
sign-and-package:
  environment: production  # Uses production secrets
```

## Audit Trail

All certificate operations are logged:
- GitHub Actions logs show when signing occurred
- Signature info is published with each release
- Timestamp server provides independent proof of signing time

Review logs periodically for unauthorized signing attempts.
