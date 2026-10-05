<p align="center">
  <a href="README.md">English</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
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

**Windows版Ollamaチャット。** デフォルトでは、このPCで実行されます。必要に応じて、SSH経由でレンタルしたGPUを使用できます。

InControlはOllama HTTP APIに対応しています。レンタル環境に接続するまで、データは送信されません。上部のバーには、接続されたマシンの名前が表示され、チャットがこのPCから実行されることが示されます。

## なぜInControlを使うのか？

- **まずこのPCで。** プロンプトは、レンタルしたGPUに接続するまで、このPCに保存されます。
- **どちらの場合も同じOllamaを使用。** レンタル環境でOllamaが`127.0.0.1:11434`上で実行されます。InControlはSSHローカルフォワードを使用してアクセスします。HTTPクライアントはパブリックポートに直接アクセスしません。
- **ツール許可リストではありません。** 接続性は、アシスタントツールのURLを制御します。チャットがどこで実行されるかを決定するものではありません。
- **プロジェクトとメモ。** セッションを、独自の指示を持つプロジェクトに保存します。プロジェクトまたはセッションのメモを記憶するように指示すると、次のメッセージに一致するメモが追加されます。
- **ウェブ検索（有効にした場合）。** ウェブボタンを使用すると、ツールを使用できるモデルがDuckDuckGoを検索し、パブリックページを読み取ることができます。デフォルトではオフになっており、有効にすると、各応答に実行された検索がリスト表示されます。
- **ファイルの添付。** テキストファイルやコードファイル、またはgemma3やllama3.2-visionなどのビジョンモデル用の画像をメッセージに追加できます。クリップアイコン、Ctrl+Shift+Oキー、またはファイルをコンポーザーにドラッグ＆ドロップして使用します。
- **このPCでの音声。** 応答は、このPCで実行されるKokoroによって読み上げることができます。音声モデルは、最初に音声が必要になったときに1回ダウンロードされます（応答が読み上げられるとき、または設定を開いたとき）。
- **WinUI 3。** ネイティブWindowsアプリです。応答はMarkdownでレンダリングされます（強調、コードブロック、リスト、テーブル、リンク）。
- **ポリシーエンジン。** 組織、チーム、ユーザーのポリシー文書が、ツール、メモリ、および接続を管理します。
- **接続モード。** オフライン専用、アシストあり、または接続あり。監査ログも記録されます。

## NuGetパッケージ

コアライブラリは、独自のローカルAI統合を構築するためのスタンドアロンのNuGetパッケージとして提供されます。

| パッケージ | バージョン | 説明 |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | ローカルAIチャットアプリケーションのドメインモデル、会話タイプ、および共有抽象化。 |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | ストリーミングチャット、モデル管理、およびヘルスチェックを備えたLLMバックエンド抽象化レイヤー。Ollamaの実装が含まれます。 |

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

## ターゲットハードウェア

| コンポーネント | 最小 | 推奨 |
|-----------|---------|-------------|
| GPU | RTX 3060 (8GB) | RTX 4080/5080 (16GB) |
| RAM | 16GB | 32GB |
| OS | Windows 10 バージョン 2004 (x64) | Windows 11 |
| .NET | 9.0 | 9.0 |

## インストール

**Microsoft Store:** [InControl-Desktop](https://apps.microsoft.com/detail/9N1FG39JWF83)。ストアパッケージには、独自の.NETおよびWindows App SDKランタイムが含まれており、ストアによって自動的に更新されます。

**ポータブルダウンロード:** `InControl-<version>-win-x64-portable.zip`を[GitHub Releases](https://github.com/mcp-tool-shop-org/incontrol/releases/latest)からダウンロードします。任意の場所に解凍して`InControl.App.exe`を実行します。ランタイムは内部に含まれているため、インストールは不要です。コード署名されていないため、Windowsは初回実行時に確認を求める場合があります。`.sha256`ファイルが隣にあります。

**ソースコードから:**

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build

# Run (Ollama on this PC)
dotnet run --project src/InControl.App
```

## 前提条件

InControlには、ローカルLLMバックエンドが必要です。 [Ollama](https://ollama.ai/)の使用をお勧めします。

```bash
# Install Ollama from https://ollama.ai/download

# Pull a model
ollama pull llama3.2

# Start the server (runs on http://127.0.0.1:11434)
ollama serve
```

## レンタルGPU

設定 → **このチャットの実行場所。** レンタル環境から直接SSHコマンドを貼り付けます（`ssh -p <mapped-port> root@<public-ip> -i <key>`）。ボタンをクリックすると、チャットがそのマシンに送信されます。

フォワードは、このPCの`127.0.0.1:11436`でリッスンし、`11434`ではリッスンしません。Ollamaがそのポート経由で応答するまで、チャットはここに残ります。

レンタル環境のOllamaを`127.0.0.1:11434`で実行したままにします。`OLLAMA_HOST=0.0.0.0`を設定せず、ポート11434を公開しないでください。SSHポートは、マッピングされたsshdポートであり、11434ではありません。

RunPodの`ssh.runpod.io`プロキシは、単なるシェルです。ポートをフォワードすることはできません。「**RunPodポッドを検索**」は、環境の`RUNPOD_API_KEY`を使用し、ポッドの直接パブリックIP SSHを埋めます。キーは保存されません。検索はポッドを開始せず、チャットを送信しません。Vastは、直接アドレスでのフォワードをドキュメント化しています。アドレスは、レンタルが再起動すると失われます。ポッドを再度検索するか、新しいコマンドを貼り付けます。プライベートキーは、このPCに保存されます。

完全なルールは、[docs/COMPUTE.md](docs/COMPUTE.md)にあります。

## ビルド

### ビルド環境の確認

```powershell
# Run verification script
./scripts/verify.ps1
```

### 開発ビルド

```bash
dotnet build
```

### リリースビルド

```powershell
# Creates release artifacts in artifacts/
./scripts/release.ps1
```

### テストの実行

```bash
dotnet test
```

## アーキテクチャ

InControlは、クリーンで階層化されたアーキテクチャに従います。

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

詳細な設計ドキュメントについては、[ARCHITECTURE.md](./docs/ARCHITECTURE.md)を参照してください。

### 主要なサブシステム

| サブシステム | 名前空間 | 目的 |
|-----------|-----------|---------|
| アシスタント | `InControl.Core.Assistant` | プロファイル、メモリストア、パーソナリティガード、オンボーディング |
| プラグイン | `InControl.Core.Plugins` | マニフェストで検証された、サンドボックス化された拡張機能（SDK付き） |
| ポリシー | `InControl.Core.Policy` | JSONポリシー文書（組織/チーム/ユーザー）、ツール/プラグイン/メモリ/接続ルール |
| 接続性 | `InControl.Core.Connectivity` | 監査証跡を備えた3つのモードのネットワークガバナンス |
| ヘルスチェック | `InControl.Services.Health` | プラグ可能なヘルスチェック（アプリ、推論、ストレージ） |
| 診断 | `InControl.Core.Diagnostics` | サニタイズされた構成を含む、サポートバンドルの作成 |

## データストレージ

すべてのデータはローカルに保存されます。

| データ | 場所 |
|------|----------|
| セッション | `%LOCALAPPDATA%\InControl\sessions\` |
| プロジェクトとメモ | `%LOCALAPPDATA%\InControl\` |
| ログ | `%LOCALAPPDATA%\InControl\logs\` |
| キャッシュ（音声モデル） | `%LOCALAPPDATA%\InControl\cache\` |
| エクスポート | `%USERPROFILE%\Documents\InControl\exports\` |

ストアインストールは、そのフォルダーをアプリ自体のストレージ内に保持する場合があります。Windowsはアンインストール時にそれを削除します。

プライバシーポリシーは[PRIVACY.md](./PRIVACY.md)です。データ処理の詳細については、[docs/PRIVACY.md](./docs/PRIVACY.md)を参照してください。

## トラブルシューティング

一般的な問題とその解決策は、[TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md)に記載されています。

### クイックフィックス

**アプリが起動しない（ソースビルドの場合）：**
- .NET 9 SDKがインストールされていることを確認してください：`dotnet --list-sdks`
- ストアパッケージには、個別のランタイムは必要ありません。

**利用可能なモデルがない：**
- Ollamaが実行されていることを確認してください：`ollama serve`
- モデルをダウンロードしてください：`ollama pull llama3.2`

**応答が遅い：**
- OllamaがモデルをGPUで実行するかどうかを決定します。`ollama ps`は、そのうちGPUで実行される割合を示します。
- GPUドライバーを更新するか、より小さいモデルをダウンロードしてください。

## 貢献

ご協力をお待ちしております！以下の点にご注意ください。

1. リポジトリをフォークします。
2. 機能ブランチを作成します。
3. 新しい機能のテストを記述します。
4. プルリクエストを送信します。

## 問題の報告

1. まず、[TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md)を確認してください。
2. アプリの「診断情報のコピー」機能を使用します。
3. 診断情報付きで問題を報告してください。

## 技術スタック

| レイヤー | 技術 |
|-------|------------|
| UIフレームワーク | WinUI 3（Windows App SDK 1.6） |
| アーキテクチャ | CommunityToolkit.Mvvmを使用したMVVM |
| LLM統合 | OllamaSharp、Microsoft.Extensions.AI |
| DIコンテナ | Microsoft.Extensions.DependencyInjection |
| 構成 | Microsoft.Extensions.Configuration |
| ロギング | Microsoft.Extensions.Logging + Serilog |

## バージョン

現在のバージョン：**2.0.1**。Microsoft Storeでは、InControl-Desktopという名前で、パッケージIDは`mcp-tool-shop.InControl-Desktop`、バージョンは`2.0.1.0`です。ストアにはすでに1.4.0のこのアプリが存在するため、パッケージバージョンはそれよりも大きい値から始まります。リポジトリ、ウィンドウ、およびスタートタイルに表示される名前はInControlです。

バージョンが変更された理由と、0.3.0の履歴については、[CHANGELOG.md](./CHANGELOG.md)を参照してください。

## セキュリティとデータ範囲

InControlは、Ollama用のWinUI 3チャットアプリケーションです。

- **アクセスされるデータ：** このPC上のOllama、チャット履歴、ローカルストレージ内のプロジェクトとメモ、および接続後にSSH経由でアクセスするマシンのOllama。
- **アクセスされないデータ：** InControlアカウント、テレメトリ、分析。
- **権限：** OllamaへのループバックHTTP、レンタルに接続したときのこのPC上のSSHクライアント、およびチャット履歴用のファイルシステム。

完全なポリシー：[SECURITY.md](SECURITY.md)

---

## サポート

- **バグレポート：** [Issues](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **セキュリティ：** [SECURITY.md](SECURITY.md)

## ライセンス

[MIT](LICENSE) - 詳細については、[LICENSE](LICENSE)を参照してください。

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
