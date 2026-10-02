# Troubleshooting Guide

**InControl-Desktop — Common Issues and Solutions**

---

## Quick Diagnostics

Use the built-in diagnostics feature to gather system information:

1. Open InControl
2. Go to Settings > About
3. Click "Copy Diagnostics" to copy info to clipboard
4. Include this when reporting issues

---

## Startup Issues

### App Won't Start

**Symptoms:** App crashes immediately or shows no window.

**Solutions:**

1. **Check .NET Runtime**
   ```bash
   dotnet --list-runtimes
   ```
   Ensure `Microsoft.WindowsDesktop.App 9.0.x` is installed.

2. **Install Windows App SDK**
   The app requires Windows App SDK 1.6+. Download from:
   https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads

3. **Check Windows Version**
   InControl requires Windows 10 version 1809 (build 17763) or later.
   ```powershell
   winver
   ```

4. **Check Event Viewer**
   - Open Event Viewer
   - Navigate to Windows Logs > Application
   - Look for errors from "InControl"

### App Starts But Shows Blank Window

**Symptoms:** Window appears but content is missing.

**Solutions:**

1. **Update GPU Drivers**
   WinUI 3 requires up-to-date GPU drivers for rendering.

2. **Disable Hardware Acceleration** (temporary)
   Add to `appsettings.json`:
   ```json
   {
     "Rendering": { "UseHardwareAcceleration": false }
   }
   ```

3. **Check Display Scaling**
   Try setting display scaling to 100% temporarily.

---

## GPU Issues

### GPU Not Detected

**Symptoms:** App reports "No GPU available" or uses CPU inference.

**Solutions:**

1. **Update NVIDIA Drivers**
   - Download from: https://www.nvidia.com/drivers
   - Minimum version: 535.x for RTX cards

2. **Verify CUDA Installation**
   ```bash
   nvidia-smi
   ```
   Should show your GPU and CUDA version.

3. **Check GPU Memory**
   - 7B models need ~4GB VRAM
   - 13B models need ~8GB VRAM
   - 70B models need ~40GB VRAM (quantized: ~35GB)

### CUDA Out of Memory

**Symptoms:** Model loading fails with memory error.

**Solutions:**

1. **Use a smaller model**
   ```bash
   ollama pull llama3.2:1b  # 1B parameter model
   ```

2. **Close other GPU applications**
   - Check Task Manager > GPU
   - Close browsers with GPU acceleration
   - Close other AI applications

3. **Use quantized models**
   ```bash
   ollama pull llama3.2:7b-q4_0  # 4-bit quantized
   ```

### Slow Inference

**Symptoms:** Model responses are very slow.

**Solutions:**

1. **Check if using GPU**
   - Open Task Manager
   - Go to Performance > GPU
   - Check "3D" or "CUDA" usage during inference

2. **Check thermal throttling**
   - Use HWiNFO or GPU-Z to monitor temperatures
   - Ensure adequate cooling

3. **Try different models**
   Some models are better optimized for specific GPUs.

---

## Model Issues

### Model Not Found

**Symptoms:** "Model not found" error when starting chat.

**Solutions:**

1. **Check Ollama is running**
   ```bash
   curl http://localhost:11434/api/tags
   ```

2. **Pull the model**
   ```bash
   ollama pull llama3.2
   ```

3. **List available models**
   ```bash
   ollama list
   ```

### Model Download Fails

**Symptoms:** Model download hangs or fails.

**Solutions:**

1. **Check disk space**
   Models can be 4-50GB. Ensure sufficient space in:
   - `%USERPROFILE%\.ollama\models\`

2. **Check network connectivity**
   ```bash
   curl -I https://ollama.ai
   ```

3. **Retry with verbose output**
   ```bash
   OLLAMA_DEBUG=1 ollama pull llama3.2
   ```

---

## Connection Issues

### Cannot Connect to Backend

**Symptoms:** "Connection refused" or "Host not found" errors.

**Solutions:**

1. **Start Ollama**
   ```bash
   ollama serve
   ```

2. **Check port availability**
   ```powershell
   netstat -an | findstr 11434
   ```

3. **Check firewall**
   - Windows Security > Firewall
   - Allow "Ollama" through firewall

4. **Verify endpoint**
   Default: `http://localhost:11434`
   Check Settings > Inference Backend

### Connection Timeout

**Symptoms:** Requests hang and eventually time out.

**Solutions:**

1. **Increase timeout**
   In Settings > Advanced, increase connection timeout.

2. **Check system resources**
   - High CPU/memory usage can cause delays
   - Close unnecessary applications

---

## Data & Storage Issues

### Session Data Corrupted

**Symptoms:** App crashes when loading sessions, or sessions are missing.

**Solutions:**

1. **Check state health**
   The app automatically detects corrupt files on startup.

2. **Quarantine corrupt files**
   Corrupt files are moved to:
   `%LOCALAPPDATA%\InControl\quarantine\`

3. **Restore from backup**
   If you have a backup in:
   `%LOCALAPPDATA%\InControl\backup\`

4. **Reset application**
   Settings > Advanced > Reset Application
   (Exports data before reset)

### Disk Full

**Symptoms:** Save operations fail, app becomes unresponsive.

**Solutions:**

1. **Clear cache**
   Delete contents of:
   `%LOCALAPPDATA%\InControl\cache\`

2. **Clean old logs**
   Delete old logs from:
   `%LOCALAPPDATA%\InControl\logs\`

3. **Export and delete old sessions**
   Export important sessions, then delete old ones.

---

## Export/Import Issues

### Export Fails

**Symptoms:** Export button does nothing or shows error.

**Solutions:**

1. **Check write permissions**
   Ensure you can write to:
   `%USERPROFILE%\Documents\InControl\exports\`

2. **Check disk space**
   Large conversations need space for export.

3. **Try different format**
   If JSON fails, try Markdown export.

### Import Fails

**Symptoms:** Imported file not recognized.

**Solutions:**

1. **Check file format**
   Only `.json` files from InControl export are supported.

2. **Check file integrity**
   Open the JSON file in a text editor to verify it's valid.

3. **Check version compatibility**
   Exports from much older versions may not be compatible.

---

## Performance Issues

### High Memory Usage

**Symptoms:** App uses excessive RAM over time.

**Solutions:**

1. **Restart the app**
   Memory is freed when the app closes.

2. **Clear conversation history**
   Very long conversations consume memory.

3. **Check for memory leaks**
   Report consistent memory growth to maintainers.

### UI Lag

**Symptoms:** Interface is slow or unresponsive.

**Solutions:**

1. **Reduce conversation length**
   Very long conversations (10k+ messages) may cause lag.

2. **Disable animations**
   Settings > Appearance > Reduce Animations

3. **Check GPU driver**
   WinUI 3 uses GPU for rendering.

---

## Getting Help

If your issue isn't covered here:

1. **Gather diagnostics**
   - Copy diagnostics from Settings > About
   - Export support bundle if available

2. **Search existing issues**
   https://github.com/mcp-tool-shop-org/InControl-Desktop/issues

3. **Open new issue**
   Include:
   - Diagnostics info
   - Steps to reproduce
   - Expected vs actual behavior
   - Screenshots if applicable

---

## Adding New Diagnostics

For developers adding diagnostic capabilities:

1. Add to `DiagnosticsInfo.cs`:
   ```csharp
   public static class DiagnosticsInfo
   {
       // Add new diagnostic methods here
   }
   ```

2. Include in support bundle via `SupportBundle.cs`

3. Add tests in `DiagnosticsInfoTests.cs`

---

*Last updated: 2026-02-03*
