# Support

## Getting Help

### Documentation

- [Installation Guide](./docs/INSTALLATION.md) - Build 2.0.0 from source. There is no MSIX in this repository. A later package identity mcp-tool-shop.InControl-Desktop 2.0.0.0 is being prepared. Tag v0.3.0 is an older source-only release.
- [Release Charter](./docs/RELEASE_CHARTER.md) - Older plan, not the current ship. See [Connectivity](./docs/CONNECTIVITY.md).
- [Connectivity Guide](./docs/CONNECTIVITY.md) - Network feature documentation

### Self-Help Resources

Before opening an issue, please try:

1. **Check the FAQ** below
2. **Search existing issues** for similar problems
3. **Review the logs** at `%LOCALAPPDATA%\InControl\logs`
4. **Try a clean reinstall** if issues persist

---

## Frequently Asked Questions

### General

**Q: Does InControl require internet access?**

A: Not for a chat on this PC. Ollama on this machine is the default. Connecting a rented GPU uses SSH, and that chat leaves this PC until you choose **Stay on this PC**. The Connectivity page is a separate allowlist for assistant tool URLs.

**Q: Where is my data stored?**

A: On this PC:
- Conversations: `%LOCALAPPDATA%\InControl\sessions\`
- Settings: `%LOCALAPPDATA%\InControl\config\`
- Logs: `%LOCALAPPDATA%\InControl\logs\`
- SSH known hosts for a rental: `%LOCALAPPDATA%\InControl\ssh\`

**Q: Can I export my conversations?**

A: Yes. Go to Settings → Export to save your conversations as JSON files.

### Ollama Connection

**Q: Why can't InControl connect to Ollama?**

A: Common causes:
1. Ollama isn't running (`ollama serve` in terminal)
2. Ollama is on a different port (check Settings → Ollama URL)
3. Firewall blocking localhost connections
4. Ollama crashed (check Ollama logs)

**Q: Which models work with InControl?**

A: Any Ollama-compatible model. Recommended:
- `llama3.2` - Good balance of speed and quality
- `qwen2.5` - Excellent for coding tasks
- `mistral` - Fast responses

### Updates

**Q: How do I update InControl?**

A: Check out a newer commit and build again. See [Installation](./docs/INSTALLATION.md). There is no installer to download over the old one. Settings under `%LOCALAPPDATA%\InControl\` stay where they are.

**Q: Can I go back to a previous version?**

A: Yes. Previous versions are always available in the Releases archive. Note:
- Settings may need to reset if formats changed
- Conversations are preserved across versions

### Performance

**Q: Why is generation slow?**

A: Check:
1. Your GPU is being used (Settings → Show GPU stats)
2. Model isn't too large for your VRAM
3. No other GPU-intensive apps running

**Q: How much disk space does InControl need?**

A: The app itself is ~50MB. Models are stored by Ollama separately (1-8GB per model).

---

## Reporting Issues

### Before Reporting

1. Update to the latest version
2. Check if the issue is already reported
3. Gather diagnostic information:
   - InControl version (Help → About)
   - Windows version
   - Ollama version
   - Relevant log files

### How to Report

1. Go to [GitHub Issues](../../issues)
2. Click "New Issue"
3. Choose the appropriate template
4. Provide:
   - Clear description of the problem
   - Steps to reproduce
   - Expected vs actual behavior
   - Log snippets if relevant

### What NOT to Include

- Personal conversations or data
- API keys or tokens
- Screenshots with sensitive information

---

## Feature Requests

We welcome feature suggestions:

1. Check if it's already requested in Issues
2. Open a new issue with the "Feature Request" template
3. Describe:
   - The problem you're trying to solve
   - Your proposed solution
   - Alternative approaches you considered

---

## Security Vulnerabilities

**Do NOT report security vulnerabilities through public issues.**

Instead:
1. Email security concerns to [security contact]
2. Include detailed steps to reproduce
3. Allow time for us to address before public disclosure

---

## Community Guidelines

When seeking help:

- Be respectful and patient
- Provide complete information
- Search before asking
- Help others when you can

---

## Response Times

This is a community project. Response times vary:

- Critical bugs: Best effort within days
- General issues: Within a week typically
- Feature requests: Reviewed monthly

Thank you for using InControl-Desktop!
