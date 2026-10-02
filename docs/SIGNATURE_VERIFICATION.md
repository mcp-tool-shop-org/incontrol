# Signature Verification

Version 0.3.0 does not ship a signed MSIX. There is no `InControl-Desktop-x.y.z.msix` to right-click, and `signtool` has nothing to verify.

Tag `v0.3.0` on [mcp-tool-shop-org/incontrol](https://github.com/mcp-tool-shop-org/incontrol) is an older source-only release. Current main is app 2.0.0 and is not that commit. There is still no signed MSIX in this repository. Install the current tree from `docs/INSTALLATION.md`.

To look at the older tag:

```bash
git fetch --tags
git rev-parse v0.3.0
```

The steps that used to say "download the MSIX and open Digital Signatures" were for an installer this repository does not publish. Do not follow them.
