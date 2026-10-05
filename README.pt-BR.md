<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.md">English</a>
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

**Ollama chat para Windows.** Neste computador, por padrão. Uma GPU que você alugou, via SSH, quando solicitado.

O InControl utiliza a API HTTP do Ollama. Nada é enviado para um servidor alugado até que você conecte um. A barra na parte superior, então, identifica essa máquina e indica que o chat está sendo executado neste computador.

## Por que InControl?

- **Este computador primeiro.** Os prompts permanecem aqui até que você conecte uma GPU alugada.
- **O mesmo Ollama, de qualquer forma.** Um servidor alugado executa o Ollama em `127.0.0.1:11434`. O InControl acessa-o com um encaminhamento SSH local. O cliente HTTP nunca se comunica com uma porta pública.
- **Não é a lista de ferramentas permitidas.** A conectividade controla as URLs das ferramentas de assistência. Não determina onde o chat é executado.
- **Projetos e notas.** Salve as sessões em projetos com suas próprias instruções. Peça para ele lembrar uma nota para um projeto ou sessão, e as notas correspondentes serão incluídas na próxima mensagem.
- **Pesquisa na web, quando você ativar.** O botão da web permite que um modelo que pode usar ferramentas pesquise no DuckDuckGo e leia páginas públicas. Ele está desativado até que você o ative, e cada resposta lista as pesquisas que foram feitas.
- **Anexar arquivos.** Adicione arquivos de texto e código a uma mensagem, ou imagens para um modelo de visão, como gemma3 ou llama3.2-vision. Use o clipe de papel, Ctrl+Shift+O ou arraste os arquivos para o editor.
- **Voz neste computador.** As respostas podem ser lidas em voz alta por Kokoro, que é executado neste computador. O modelo de voz é baixado uma vez, na primeira vez que a voz é necessária: quando uma resposta é falada ou quando você abre as Configurações.
- **WinUI 3.** Um aplicativo Windows nativo. As respostas renderizam markdown: ênfase, blocos de código, listas, tabelas e links.
- **Mecanismo de política.** Documentos de política de organização, equipe e usuário governam ferramentas, memória e conectividade.
- **Modos de conectividade.** Apenas offline, assistido ou conectado, com um registro de auditoria.

## Pacotes NuGet

As bibliotecas principais estão disponíveis como pacotes NuGet independentes para criar suas próprias integrações locais de IA:

| Pacote | Versão | Descrição |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | Modelos de domínio, tipos de conversação e abstrações compartilhadas para aplicativos de chat de IA locais. |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | Camada de abstração de back-end LLM com chat em streaming, gerenciamento de modelo e verificações de integridade. Inclui a implementação do Ollama. |

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

## Hardware de destino

| Componente | Mínimo | Recomendado |
|-----------|---------|-------------|
| GPU | RTX 3060 (8GB) | RTX 4080/5080 (16GB) |
| RAM | 16GB | 32GB |
| OS | Windows 10 versão 2004 (x64) | Windows 11 |
| .NET | 9.0 | 9.0 |

## Instalação

**Microsoft Store:** [InControl-Desktop](https://apps.microsoft.com/detail/9N1FG39JWF83). O pacote da Store carrega seus próprios runtimes .NET e Windows App SDK, e a Store mantém tudo atualizado.

**Download portátil:** `InControl-<version>-win-x64-portable.zip` em [GitHub Releases](https://github.com/mcp-tool-shop-org/incontrol/releases/latest). Descompacte em qualquer lugar e execute `InControl.App.exe`; os runtimes estão dentro, então nada é instalado. Não é assinado digitalmente, então o Windows pode pedir para você confirmar na primeira execução. Um arquivo `.sha256` está ao lado.

**A partir do código-fonte:**

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build

# Run (Ollama on this PC)
dotnet run --project src/InControl.App
```

## Pré-requisitos

O InControl requer um back-end LLM local. Recomendamos [Ollama](https://ollama.ai/):

```bash
# Install Ollama from https://ollama.ai/download

# Pull a model
ollama pull llama3.2

# Start the server (runs on http://127.0.0.1:11434)
ollama serve
```

## Uma GPU alugada

Configurações → **Onde este chat é executado.** Cole o comando SSH direto do servidor alugado (`ssh -p <mapped-port> root@<public-ip> -i <key>`). O botão indica que o chat será enviado para essa máquina.

O encaminhamento escuta em `127.0.0.1:11436` neste computador, não em `11434`. O chat permanece aqui até que o Ollama responda por meio dessa porta.

Deixe o Ollama em `127.0.0.1:11434` no servidor alugado. Não defina `OLLAMA_HOST=0.0.0.0` e não publique a porta 11434. A porta SSH é a porta sshd mapeada, não 11434.

O proxy `ssh.runpod.io` da RunPod é apenas um shell. Não pode encaminhar uma porta. **Localizar meus pods RunPod** usa `RUNPOD_API_KEY` do ambiente e preenche o endereço SSH público direto do pod. A chave não é armazenada. A pesquisa não inicia um pod e não envia o chat. A Vast documenta o encaminhamento no endereço direto. O endereço expira quando o servidor alugado é reiniciado. Localize o pod novamente ou cole o novo comando. A chave privada permanece neste computador.

As regras completas estão em [docs/COMPUTE.md](docs/COMPUTE.md).

## Construção

### Verificar o ambiente de construção

```powershell
# Run verification script
./scripts/verify.ps1
```

### Construção de desenvolvimento

```bash
dotnet build
```

### Construção de lançamento

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

| Subsistema | Namespace | Propósito |
|-----------|-----------|---------|
| Assistente | `InControl.Core.Assistant` | Perfis, armazenamento de memória, proteção de personalidade, integração |
| Plugins | `InControl.Core.Plugins` | Extensibilidade com SDK, validada por manifesto e com sandbox |
| Política | `InControl.Core.Policy` | Documentos de política JSON (organização/equipe/usuário), regras de ferramentas/plugins/memória/conectividade |
| Conectividade | `InControl.Core.Connectivity` | Governança de rede de três modos com trilha de auditoria |
| Saúde | `InControl.Services.Health` | Verificações de integridade conectáveis (aplicativo, inferência, armazenamento) |
| Diagnóstico | `InControl.Core.Diagnostics` | Criação de pacote de suporte com configurações sanitizadas |

## Armazenamento de dados

Todos os dados são armazenados localmente:

| Dados | Localização |
|------|----------|
| Sessões | `%LOCALAPPDATA%\InControl\sessions\` |
| Projetos e notas | `%LOCALAPPDATA%\InControl\` |
| Logs | `%LOCALAPPDATA%\InControl\logs\` |
| Cache (modelo de voz) | `%LOCALAPPDATA%\InControl\cache\` |
| Exportações | `%USERPROFILE%\Documents\InControl\exports\` |

Uma instalação da Store pode manter essa pasta dentro do próprio armazenamento do aplicativo, que o Windows remove ao desinstalar.

A política de privacidade está em [PRIVACY.md](./PRIVACY.md). Os detalhes sobre o tratamento de dados estão em [docs/PRIVACY.md](./docs/PRIVACY.md).

## Solução de problemas

Problemas comuns e soluções estão documentados em [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md).

### Correções rápidas

**O aplicativo não inicia (compilação da fonte):**
- Verifique se o SDK .NET 9 está instalado: `dotnet --list-sdks`
- O pacote da loja não precisa de um tempo de execução separado

**Nenhum modelo disponível:**
- Certifique-se de que o Ollama está em execução: `ollama serve`
- Baixe um modelo: `ollama pull llama3.2`

**As respostas estão lentas:**
- O Ollama decide se o modelo é executado na GPU. `ollama ps` mostra quanta parte dele está na GPU
- Atualize o driver da GPU ou baixe um modelo menor

## Contribuições

Contribuições são bem-vindas! Por favor:

1. Faça um fork do repositório
2. Crie um branch de recurso
3. Escreva testes para a nova funcionalidade
4. Envie um pull request

## Relato de problemas

1. Verifique [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md) primeiro
2. Use o recurso "Copiar diagnósticos" no aplicativo
3. Abra um problema com as informações de diagnóstico anexadas

## Pilhas de tecnologia

| Camada | Tecnologia |
|-------|------------|
| Framework de UI | WinUI 3 (Windows App SDK 1.6) |
| Arquitetura | MVVM com CommunityToolkit.Mvvm |
| Integração LLM | OllamaSharp, Microsoft.Extensions.AI |
| Contêiner DI | Microsoft.Extensions.DependencyInjection |
| Configuração | Microsoft.Extensions.Configuration |
| Registro | Microsoft.Extensions.Logging + Serilog |

## Versão

Versão atual: **2.0.1**. Na Microsoft Store, é InControl-Desktop, identidade do pacote `mcp-tool-shop.InControl-Desktop`, versão `2.0.1.0`. A loja já tinha este aplicativo na versão 1.4.0, então a versão do pacote começa acima dela. O nome no repositório, na janela e no bloco Iniciar é InControl.

Veja [CHANGELOG.md](./CHANGELOG.md) para entender por que a versão aumentou e para o histórico da versão 0.3.0.

## Segurança e escopo de dados

InControl é um aplicativo de bate-papo WinUI 3 para Ollama.

- **Dados acessados:** Ollama neste PC, histórico de bate-papo, projetos e notas no armazenamento local e, somente após você conectar um, Ollama em uma máquina que você acessa via SSH
- **Dados não acessados:** Nenhuma conta InControl, nenhuma telemetria, nenhuma análise
- **Permissões:** HTTP de loopback para Ollama, um cliente SSH neste PC quando você conecta um servidor e o sistema de arquivos para o histórico de bate-papo

Política completa: [SECURITY.md](SECURITY.md)

---

## Suporte

- **Relatos de bugs:** [Issues](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **Segurança:** [SECURITY.md](SECURITY.md)

## Licença

[MIT](LICENSE) -- veja [LICENSE](LICENSE) para o texto completo.

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
