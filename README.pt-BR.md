<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.md">English</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/incontrol/readme.png" alt="InControl" width="400"></p>

<h1 align="center">InControl</h1>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-9-purple?style=flat-square&logo=dotnet" alt=".NET 9">
  <img src="https://img.shields.io/badge/WinUI-3-blue?style=flat-square" alt="WinUI 3">
  <a href="https://github.com/mcp-tool-shop-org/incontrol/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/incontrol/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://codecov.io/gh/mcp-tool-shop-org/incontrol"><img src="https://codecov.io/gh/mcp-tool-shop-org/incontrol/branch/main/graph/badge.svg" alt="Coverage"></a>
  <a href="https://mcp-tool-shop-org.github.io/incontrol/"><img src="https://img.shields.io/badge/docs-handbook-blue?style=flat-square" alt="Handbook"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License"></a>
</p>

**Ollama chat para Windows.** Neste computador, por padrão. Uma GPU que você alugou, via SSH, quando solicitado.

O InControl utiliza a API HTTP do Ollama. Nada é enviado para um servidor alugado até que você conecte um. A barra na parte superior, então, identifica essa máquina e diz que o chat sai deste computador.

## Por que InControl?

- **Este computador primeiro.** Os prompts permanecem aqui até que você conecte uma GPU alugada.
- **O mesmo Ollama em ambos os casos.** Um servidor alugado executa o Ollama em `127.0.0.1:11434`. O InControl acessa-o com um encaminhamento SSH local. O cliente HTTP nunca se comunica com uma porta pública.
- **Não é a lista de permissões de ferramentas.** A conectividade controla as URLs das ferramentas de assistência. Não determina onde o chat é executado.
- **WinUI 3.** Um aplicativo Windows, com formatação Markdown no chat.
- **Perfis de assistente:** Personalidade, verbosidade e tolerância ao risco configuráveis.
- **Sistema de plugins:** Expanda a funcionalidade com plugins isolados e um SDK baseado em manifesto.
- **Mecanismo de política:** Camadas de política de organização/equipe/usuário governam ferramentas, plugins, memória e conectividade.
- **Modos de conectividade:** Apenas offline, assistido ou conectado com registro completo de auditoria.

## Pacotes NuGet

As bibliotecas principais estão disponíveis como pacotes NuGet independentes para criar suas próprias integrações locais de IA:

| Pacote | Versão | Descrição |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | Modelos de domínio, tipos de conversação e abstrações compartilhadas para aplicativos de chat de IA local. |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | Camada de abstração do backend LLM com chat em streaming, gerenciamento de modelo e verificações de integridade. Inclui a implementação do Ollama. |

```bash
dotnet add package InControl.Core
dotnet add package InControl.Inference
```

```csharp
// Example: use InControl.Inference in your own app
var client = inferenceClientFactory.Create("ollama");
await foreach (var token in client.StreamChatAsync(messages))
{
    Console.Write(token);
}
```

## Hardware de destino

| Componente | Mínimo | Recomendado |
|-----------|---------|-------------|
| GPU | RTX 3060 (8 GB) | RTX 4080/5080 (16 GB) |
| RAM | 16 GB | 32 GB |
| OS | Windows 10 1809+ | Windows 11 |
| .NET | 9.0 | 9.0 |

## Instalação

Compile a partir do código-fonte. Ainda não há uma versão MSIX deste repositório.

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build

# Run (Ollama on this PC)
dotnet run --project src/InControl.App
```

## Pré-requisitos

O InControl requer um backend LLM local. Recomendamos o [Ollama](https://ollama.ai/):

```bash
# Install Ollama from https://ollama.ai/download

# Pull a model
ollama pull llama3.2

# Start the server (runs on http://127.0.0.1:11434)
ollama serve
```

## Uma GPU alugada

Configurações → **Onde este chat é executado**. Cole o comando SSH direto do servidor alugado (`ssh -p <mapped-port> root@<public-ip> -i <key>`). O botão indica que o chat será enviado para essa máquina.

O encaminhamento escuta na porta `127.0.0.1:11436` neste computador, não na `11434`. O chat permanece aqui até que o Ollama responda por meio dessa porta.

Deixe o Ollama rodando na porta `127.0.0.1:11434` no servidor alugado. Não defina a porta `OLLAMA_HOST=0.0.0.0` e não publique a porta 11434. A porta SSH é a porta mapeada do sshd, não 11434.

O proxy `ssh.runpod.io` da RunPod é apenas um shell. Ele não pode encaminhar uma porta. "Localizar meus pods RunPod" usa `RUNPOD_API_KEY` do ambiente e preenche o comando SSH no IP público do pod. A chave não é armazenada. A pesquisa não inicia um pod e não envia o chat. O Vast documenta o encaminhamento no endereço direto. O endereço é desativado quando o servidor alugado é reiniciado. Localize o pod novamente ou cole o novo comando. A chave privada permanece neste computador.

As regras completas estão em [docs/COMPUTE.md](docs/COMPUTE.md).

## Compilação

### Verificar o ambiente de compilação

```powershell
# Run verification script
./scripts/verify.ps1
```

### Compilação de desenvolvimento

```bash
dotnet build
```

### Compilação de lançamento

```powershell
# Creates release artifacts in artifacts/
./scripts/release.ps1
```

### Executar testes

```bash
dotnet test
```

## Arquitetura

O InControl segue uma arquitetura limpa e em camadas:

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

Consulte [ARCHITECTURE.md](./docs/ARCHITECTURE.md) para obter documentação detalhada do design.

### Subsistemas-chave

| Subsistema | Namespace | Finalidade |
|-----------|-----------|---------|
| Assistente | `InControl.Core.Assistant` | Perfis, armazenamento de memória, proteção de personalidade, integração |
| Plugins | `InControl.Core.Plugins` | Extensibilidade validada por manifesto e isolada com SDK |
| Política | `InControl.Core.Policy` | Documentos de política JSON (organização/equipe/usuário), regras de ferramenta/plugin/memória/conectividade |
| Conectividade | `InControl.Core.Connectivity` | Governança de rede de três modos com trilha de auditoria |
| Integridade | `InControl.Services.Health` | Verificações de integridade conectáveis (aplicativo, inferência, armazenamento) |
| Diagnóstico | `InControl.Core.Diagnostics` | Criação de pacote de suporte com configurações sanitizadas |

## Armazenamento de dados

Todos os dados são armazenados localmente:

| Dados | Localização |
|------|----------|
| Sessões | `%LOCALAPPDATA%\InControl\sessions\` |
| Logs | `%LOCALAPPDATA%\InControl\logs\` |
| Cache | `%LOCALAPPDATA%\InControl\cache\` |
| Exportações | `%USERPROFILE%\Documents\InControl\exports\` |

Consulte [PRIVACY.md](./docs/PRIVACY.md) para obter documentação completa sobre o tratamento de dados.

## Solução de problemas

Problemas comuns e soluções estão documentados em [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md).

### Correções rápidas

**O aplicativo não inicia:**
- Verifique se o .NET 9.0 Runtime está instalado
- Execute `dotnet --list-runtimes` para verificar

**Nenhum modelo disponível:**
- Certifique-se de que o Ollama está em execução: `ollama serve`
- Baixe um modelo: `ollama pull llama3.2`

**GPU não detectada:**
- Atualize os drivers NVIDIA para a versão mais recente
- Verifique a instalação do CUDA Toolkit

## Contribuições

Contribuições são bem-vindas! Por favor:

1. Faça um fork do repositório
2. Crie um branch de recurso
3. Escreva testes para a nova funcionalidade
4. Envie um pull request

## Relatando problemas

1. Verifique [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md) primeiro
2. Use o recurso "Copiar diagnósticos" no aplicativo
3. Abra um problema com as informações de diagnóstico anexadas

## Conjunto de tecnologias

| Camada | Tecnologia |
|-------|------------|
| Framework de UI | WinUI 3 (Windows App SDK 1.6) |
| Arquitetura | MVVM com CommunityToolkit.Mvvm |
| Integração LLM | OllamaSharp, Microsoft.Extensions.AI |
| Contêiner DI | Microsoft.Extensions.DependencyInjection |
| Configuração | Microsoft.Extensions.Configuration |
| Registo de eventos | Microsoft.Extensions.Logging + Serilog |

## Versão

Versão atual: **0.3.0**

Consulte [CHANGELOG.md](./CHANGELOG.md) para o histórico de versões.

## Segurança e âmbito dos dados

InControl é uma aplicação de chat WinUI 3 para Ollama.

- **Dados acessados:** Ollama neste PC, histórico de chat no armazenamento local e, apenas após a conexão, Ollama em uma máquina acessada via SSH
- **Dados não acessados:** Nenhuma conta InControl, nenhum telemetria, nenhuma análise
- **Permissões:** HTTP de loopback para Ollama, um cliente SSH neste PC quando você conecta um servidor e o sistema de arquivos para o histórico de chat

Política completa: [SECURITY.md](SECURITY.md)

---

## Suporte

- **Relatórios de erros:** [Issues](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **Segurança:** [SECURITY.md](SECURITY.md)

## Licença

[MIT](LICENSE) – consulte [LICENSE](LICENSE) para o texto completo.

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
