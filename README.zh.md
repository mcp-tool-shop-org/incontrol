<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.md">English</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/incontrol/readme.png" alt="InControl" width="400"></p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-9-purple?style=flat-square&logo=dotnet" alt=".NET 9">
  <img src="https://img.shields.io/badge/WinUI-3-blue?style=flat-square" alt="WinUI 3">
  <a href="https://github.com/mcp-tool-shop-org/incontrol/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/incontrol/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://codecov.io/gh/mcp-tool-shop-org/incontrol"><img src="https://codecov.io/gh/mcp-tool-shop-org/incontrol/branch/main/graph/badge.svg" alt="Coverage"></a>
  <a href="https://apps.microsoft.com/detail/9N1FG39JWF83"><img src="https://img.shields.io/badge/Microsoft_Store-InControl--Desktop-0078D4?style=flat-square&logo=microsoft" alt="Microsoft Store"></a>
  <a href="https://mcp-tool-shop-org.github.io/incontrol/"><img src="https://img.shields.io/badge/docs-handbook-blue?style=flat-square" alt="Handbook"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License"></a>
</p>

**适用于 Windows 的 Ollama 聊天。** 默认情况下，在您的本地 PC 上运行。如果您需要，可以通过 SSH 租用 GPU。

InControl 使用 Ollama HTTP API。在您连接到租用的 GPU 之前，不会向租用服务器发送任何数据。顶部栏会显示已连接的机器的名称，并说明聊天内容将从本地 PC 发送。

## 为什么选择 InControl？

- **首先使用本地 PC。** 提示信息会保留在本地，直到您连接到租用的 GPU。
- **无论哪种方式，都使用相同的 Ollama。** 租用服务器运行 Ollama `127.0.0.1:11434`。InControl 通过 SSH 本地端口转发与其连接。HTTP 客户端不会与公共端口通信。
- **不使用工具白名单。** 连接控制助手工具的 URL。它不会决定聊天在何处运行。
- **项目和笔记。** 将会话存储在具有自己指令的项目中。您可以要求它记住某个项目或会话的笔记，并且匹配的笔记会与下一条消息一起发送。
- **网络搜索（在您启用时）。** “网络”按钮允许可以使用工具的模型搜索 DuckDuckGo 并读取公共页面。默认情况下，该功能处于关闭状态，直到您启用它，并且每次回复都会列出它执行的搜索。
- **附加文件。** 将文本和代码文件添加到消息中，或者将图像添加到视觉模型（如 gemma3 或 llama3.2-vision）中。使用回形针、Ctrl+Shift+O，或将文件拖放到编辑器中。
- **本地 PC 上的语音。** 回复可以由 Kokoro 朗读，Kokoro 在本地 PC 上运行。语音模型只需下载一次，即第一次需要语音时：当回复被朗读时，或者当您打开“设置”时。
- **WinUI 3。** 本地 Windows 应用程序。回复会渲染 Markdown：强调、代码块、列表、表格和链接。
- **策略引擎。** 组织、团队和用户策略文档控制工具、内存和连接。
- **连接模式。** 仅限离线、辅助或连接模式，并具有审核日志。

## NuGet 包

核心库作为独立的 NuGet 包提供，用于构建您自己的本地 AI 集成：

| 包 | 版本 | 描述 |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | 用于本地 AI 聊天应用程序的领域模型、对话类型和共享抽象。 |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | 具有流式聊天、模型管理和运行状况检查的 LLM 后端抽象层。包括 Ollama 实现。 |

```bash
dotnet add package InControl.Core
dotnet add package InControl.Inference
```

```csharp
// Example: use InControl.Inference in your own app
var client = inferenceClientFactory.GetClient();
var request = ChatRequest.Simple("llama3.2", "Hello");
await foreach (var token in client.StreamChatAsync(request))
{
    Console.Write(token);
}
```

## 目标硬件

| 组件 | 最低 | 推荐 |
|-----------|---------|-------------|
| GPU | RTX 3060 (8GB) | RTX 4080/5080 (16GB) |
| RAM | 16GB | 32GB |
| OS | Windows 10 版本 2004 (x64) | Windows 11 |
| .NET | 9.0 | 9.0 |

## 安装

**Microsoft Store：** [InControl-Desktop](https://apps.microsoft.com/detail/9N1FG39JWF83)。商店包自带自己的 .NET 和 Windows App SDK 运行时，并且商店会对其进行更新。

**可移植下载：** `InControl-<version>-win-x64-portable.zip` 位于 [GitHub Releases](https://github.com/mcp-tool-shop-org/incontrol/releases/latest) 上。将其解压缩到任何位置并运行 `InControl.App.exe`；运行时包含在其中，因此无需安装。它未进行代码签名，因此 Windows 可能会要求您确认首次运行。一个 `.sha256` 文件位于其旁边。

**从源代码构建：**

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build

# Run (Ollama on this PC)
dotnet run --project src/InControl.App
```

## 先决条件

InControl 需要本地 LLM 后端。我们推荐 [Ollama](https://ollama.ai/)：

```bash
# Install Ollama from https://ollama.ai/download

# Pull a model
ollama pull llama3.2

# Start the server (runs on http://127.0.0.1:11434)
ollama serve
```

## 租用的 GPU

设置 → **此聊天运行的位置。** 从租用服务器粘贴直接的 SSH 命令（`ssh -p <mapped-port> root@<public-ip> -i <key>`）。该按钮会显示聊天将被发送到该机器。

转发在本地 PC 上的 `127.0.0.1:11436` 处侦听，而不是在 `11434` 处侦听。聊天会保留在本地，直到 Ollama 通过该端口进行响应。

在租用服务器上保留 Ollama 运行在 `127.0.0.1:11434`。不要设置 `OLLAMA_HOST=0.0.0.0`，并且不要发布端口 11434。SSH 端口是映射的 sshd 端口，而不是 11434。

RunPod 的 `ssh.runpod.io` 代理只是一个 shell。它无法转发端口。**查找我的 RunPod pod** 使用 `RUNPOD_API_KEY` 从环境中获取，并填充 pod 的直接公共 IP SSH。密钥不会被存储。查找不会启动 pod，也不会发送聊天。Vast 记录了直接地址上的转发。当租用服务器重新启动时，该地址将失效。再次查找 pod，或粘贴新的命令。私钥保留在本地 PC 上。

完整的规则位于 [docs/COMPUTE.md](docs/COMPUTE.md)。

## 构建

### 验证构建环境

```powershell
# Run verification script
./scripts/verify.ps1
```

### 开发构建

```bash
dotnet build
```

### 发布构建

```powershell
# Creates release artifacts in artifacts/
./scripts/release.ps1
```

### 运行测试

```bash
dotnet test
```

## 架构

InControl 遵循清晰的分层架构：

```
+-------------------------------------------+
|         InControl.App (WinUI 3)           |  UI Layer
+-------------------------------------------+
|         InControl.ViewModels              |  Presentation
+-------------------------------------------+
|         InControl.Services                |  Business Logic
+-------------------------------------------+
|         InControl.Inference               |  LLM Backends
+-------------------------------------------+
|         InControl.Core                    |  Shared Types
+-------------------------------------------+
```

有关详细设计文档，请参阅 [ARCHITECTURE.md](./docs/ARCHITECTURE.md)。

### 关键子系统

| 子系统 | 命名空间 | 目的 |
|-----------|-----------|---------|
| 助手 | `InControl.Core.Assistant` | 配置文件、内存存储、个性保护、入职 |
| 插件 | `InControl.Core.Plugins` | 使用 SDK 进行清单验证的、沙盒化的可扩展性 |
| 策略 | `InControl.Core.Policy` | JSON 策略文档（组织/团队/用户）、工具/插件/内存/连接规则 |
| 连接 | `InControl.Core.Connectivity` | 具有审核跟踪的三种模式网络管理 |
| 运行状况 | `InControl.Services.Health` | 可插拔的运行状况检查（应用程序、推理、存储） |
| 诊断 | `InControl.Core.Diagnostics` | 使用清理后的配置创建支持包 |

## 数据存储

所有数据都存储在本地：

| 数据 | 位置 |
|------|----------|
| 会话 | `%LOCALAPPDATA%\InControl\sessions\` |
| 项目和笔记 | `%LOCALAPPDATA%\InControl\` |
| 日志 | `%LOCALAPPDATA%\InControl\logs\` |
| 缓存（语音模型） | `%LOCALAPPDATA%\InControl\cache\` |
| 导出 | `%USERPROFILE%\Documents\InControl\exports\` |

商店安装可能会将该文件夹保留在应用程序自己的存储中，Windows 会在卸载时删除该文件夹。

隐私政策位于 [PRIVACY.md](./PRIVACY.md)。数据处理详情请参见 [docs/PRIVACY.md](./docs/PRIVACY.md)。

## 故障排除

常见问题和解决方案记录在 [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md) 中。

### 快速修复

**应用程序无法启动（源代码构建）：**
- 检查是否已安装 .NET 9 SDK：`dotnet --list-sdks`
- Store 包不需要单独的运行时环境

**没有可用的模型：**
- 确保 Ollama 正在运行：`ollama serve`
- 下载一个模型：`ollama pull llama3.2`

**回复速度慢：**
- Ollama 决定模型是否在 GPU 上运行。`ollama ps` 显示有多少部分在 GPU 上运行
- 更新 GPU 驱动程序，或下载一个较小的模型

## 贡献

欢迎贡献！请：

1. 分叉仓库
2. 创建一个功能分支
3. 为新功能编写测试
4. 提交一个拉取请求

## 报告问题

1. 首先查看 [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md)
2. 使用应用程序中的“复制诊断信息”功能
3. 附上诊断信息，并创建一个问题

## 技术栈

| 层 | 技术 |
|-------|------------|
| UI 框架 | WinUI 3（Windows App SDK 1.6） |
| 架构 | 使用 CommunityToolkit.Mvvm 的 MVVM |
| LLM 集成 | OllamaSharp、Microsoft.Extensions.AI |
| DI 容器 | Microsoft.Extensions.DependencyInjection |
| 配置 | Microsoft.Extensions.Configuration |
| 日志记录 | Microsoft.Extensions.Logging + Serilog |

## 版本

当前版本：**2.0.1**。在 Microsoft Store 中，它名为 InControl-Desktop，包标识为 `mcp-tool-shop.InControl-Desktop`，版本为 `2.0.1.0`。Store 已经有这个应用程序，版本为 1.4.0，因此包版本从高于该版本开始。在仓库中、窗口中以及“开始”磁贴上的名称均为 InControl。

请参阅 [CHANGELOG.md](./CHANGELOG.md)，了解版本为何跳跃，以及 0.3.0 版本的历史记录。

## 安全与数据范围

InControl 是一款用于 Ollama 的 WinUI 3 聊天应用程序。

- **访问的数据：** 此 PC 上的 Ollama、聊天历史记录、本地存储中的项目和笔记，以及在您连接后，通过 SSH 访问的机器上的 Ollama
- **未访问的数据：** 没有 InControl 帐户，没有遥测数据，没有分析数据
- **权限：** 回环 HTTP 连接到 Ollama，当您连接到远程服务器时，此 PC 上的 SSH 客户端，以及用于聊天历史记录的文件系统

完整策略：[SECURITY.md](SECURITY.md)

---

## 支持

- **错误报告：** [Issues](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **安全：** [SECURITY.md](SECURITY.md)

## 许可证

[MIT](LICENSE) — 完整的文本请参见 [LICENSE](LICENSE)。

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
