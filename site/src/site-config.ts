import type { SiteConfig } from '@mcptoolshop/site-theme';

export const config: SiteConfig = {
  title: 'InControl',
  description: 'Ollama chat for Windows. On this PC by default, or a GPU you rented, over SSH.',
  logoBadge: 'IC',
  brandName: 'InControl',
  repoUrl: 'https://github.com/mcp-tool-shop-org/incontrol',
  footerText: 'Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>',

  hero: {
    badge: 'Open source',
    headline: 'Ollama.',
    headlineAccent: 'On your terms.',
    description: 'Chat on this PC by default. Connect a GPU you rented over SSH when you want one. The app names that machine while prompts leave this PC.',
    primaryCta: { href: 'https://apps.microsoft.com/detail/9N1FG39JWF83', label: 'Get it from the Microsoft Store' },
    secondaryCta: { href: 'handbook/', label: 'Read the Handbook' },
    previews: [
      { label: 'Install', code: 'Microsoft Store → InControl-Desktop' },
      { label: 'Build', code: 'git clone … && dotnet restore && dotnet build' },
      { label: 'Run', code: 'dotnet run --project src/InControl.App' },
    ],
  },

  sections: [
    {
      kind: 'features',
      id: 'features',
      title: 'Features',
      subtitle: 'Local AI chat that respects your privacy.',
      features: [
        { title: 'This PC first', desc: 'Prompts stay here until you connect a rental. No account and no telemetry.' },
        { title: 'SSH, not a public port', desc: 'The rental runs Ollama on 127.0.0.1. InControl opens an SSH local forward. It does not publish port 11434.' },
        { title: 'Native Windows', desc: 'WinUI 3 with Fluent Design. Looks and feels like a real Windows app, not an Electron wrapper.' },
        { title: 'Ollama', desc: 'One HTTP API, on this PC or at the near end of the tunnel. The tool-URL allowlist is a different screen.' },
        { title: 'Projects and notes', desc: 'File sessions under projects with their own instructions. Notes you ask it to remember come along with the next message that matches them.' },
        { title: 'Voice on this PC', desc: 'Kokoro reads replies aloud on this PC. The voice model downloads once, the first time it speaks.' },
        { title: 'Markdown rendering', desc: 'Rich text, code blocks, and syntax highlighting in every response.' },
        { title: 'NuGet libraries', desc: 'Core and Inference packages available on NuGet for building your own local AI integrations.' },
      ],
    },
    {
      kind: 'code-cards',
      id: 'install',
      title: 'Installation',
      cards: [
        {
          title: 'Microsoft Store',
          code: `# Search the Store for InControl-Desktop, or open
# https://apps.microsoft.com/detail/9N1FG39JWF83
# Windows 10 version 2004 or later, x64.
# The package carries its own .NET and Windows App SDK.

# Then install Ollama on this PC
# https://ollama.com/download
ollama pull llama3.2`,
        },
        {
          title: 'From Source',
          code: `git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build

# Run (Ollama on this PC)
dotnet run --project src/InControl.App`,
        },
      ],
    },
    {
      kind: 'code-cards',
      id: 'nuget',
      title: 'NuGet Packages',
      cards: [
        {
          title: 'Add packages',
          code: `dotnet add package InControl.Core
dotnet add package InControl.Inference`,
        },
        {
          title: 'Use in your app',
          code: `// Stream chat with a local LLM
var client = inferenceClientFactory.GetClient();
var request = ChatRequest.Simple("llama3.2", "Hello");
await foreach (var token in client.StreamChatAsync(request))
{
    Console.Write(token);
}`,
        },
      ],
    },
    {
      kind: 'data-table',
      id: 'hardware',
      title: 'Target Hardware',
      columns: ['Component', 'Minimum', 'Recommended'],
      rows: [
        ['GPU', 'RTX 3060 (8GB)', 'RTX 4080/5080 (16GB)'],
        ['RAM', '16GB', '32GB'],
        ['OS', 'Windows 10 version 2004 (x64)', 'Windows 11'],
        ['.NET', 'Bundled in the Store package', '9.0 SDK to build from source'],
      ],
    },
    {
      kind: 'data-table',
      id: 'architecture',
      title: 'Architecture',
      columns: ['Layer', 'Technology'],
      rows: [
        ['UI Framework', 'WinUI 3 (Windows App SDK 1.6)'],
        ['Architecture', 'MVVM with CommunityToolkit.Mvvm'],
        ['LLM Integration', 'OllamaSharp, Microsoft.Extensions.AI'],
        ['DI Container', 'Microsoft.Extensions.DependencyInjection'],
        ['Configuration', 'Microsoft.Extensions.Configuration'],
        ['Logging', 'Microsoft.Extensions.Logging + Serilog'],
      ],
    },
  ],
};
