# Signature Verification

Version 0.3.0 does not ship a signed MSIX. There is no `InControl-Desktop-x.y.z.msix` to right-click, and `signtool` has nothing to verify.

The release is the git tag `v0.3.0` on [mcp-tool-shop-org/incontrol](https://github.com/mcp-tool-shop-org/incontrol). Check that your clone is that commit:

```bash
git fetch --tags
git rev-parse v0.3.0
```

Install by building that source tree. See `docs/INSTALLATION.md`.

The steps that used to say "download the MSIX and open Digital Signatures" were for an installer this repository does not publish. Do not follow them.
