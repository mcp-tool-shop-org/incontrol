<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.md">English</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/incontrol/readme.png" alt="InControl" width="400"></p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-9-purple?style=flat-square&logo=dotnet" alt=".NET 9">
  <img src="https://img.shields.io/badge/WinUI-3-blue?style=flat-square" alt="WinUI 3">
  <a href="https://github.com/mcp-tool-shop-org/incontrol/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/incontrol/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://codecov.io/gh/mcp-tool-shop-org/incontrol"><img src="https://codecov.io/gh/mcp-tool-shop-org/incontrol/branch/main/graph/badge.svg" alt="Coverage"></a>
  <a href="https://mcp-tool-shop-org.github.io/incontrol/"><img src="https://img.shields.io/badge/docs-handbook-blue?style=flat-square" alt="Handbook"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License"></a>
</p>

**Ollama chat per Windows.** Su questo PC, per impostazione predefinita. Una GPU che hai affittato, tramite SSH, quando lo richiedi.

InControl utilizza l'API HTTP di Ollama. Nessun dato viene inviato a un server affittato finché non ne connetti uno. La barra nella parte superiore visualizza quindi il nome di tale macchina e dice che la chat lascia questo PC.

## Perché InControl?

- **Prima questo PC.** Le richieste rimangono qui finché non connetti una GPU affittata.
- **Lo stesso Ollama in entrambi i casi.** Un server affittato esegue Ollama su `127.0.0.1:11434`. InControl vi accede tramite un inoltro SSH locale. Il client HTTP non comunica mai con una porta pubblica.
- **Non è una lista di controllo degli strumenti consentiti.** La connettività controlla gli URL degli strumenti di assistenza. Non decide dove viene eseguita la chat.
- **WinUI 3.** Un'app per Windows, con markdown nel thread.
- **Profili assistente:** personalità, livello di dettaglio e tolleranza al rischio configurabili.
- **Sistema di plugin:** estende le funzionalità con plugin isolati e un SDK basato su manifest.
- **Motore di policy:** i livelli di policy dell'organizzazione/team/utente regolano gli strumenti, i plugin, la memoria e la connettività.
- **Modalità di connettività:** solo offline, assistita o connessa con registrazione completa degli eventi.

## Pacchetti NuGet

Le librerie principali sono disponibili come pacchetti NuGet autonomi per la creazione delle tue integrazioni AI locali:

| Pacchetto | Versione | Descrizione |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | Modelli di dominio, tipi di conversazione e astrazioni condivise per le applicazioni di chat AI locali. |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | Livello di astrazione del backend LLM con chat in streaming, gestione dei modelli e controlli sullo stato di salute. Include l'implementazione di Ollama. |

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

## Hardware di destinazione

| Componente | Minimo | Consigliato |
|-----------|---------|-------------|
| GPU | RTX 3060 (8 GB) | RTX 4080/5080 (16 GB) |
| RAM | 16 GB | 32 GB |
| OS | Windows 10 1809+ | Windows 11 |
| .NET | 9.0 | 9.0 |

## Installazione

Compila dal codice sorgente. Al momento non è disponibile una versione MSIX di questo repository.

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build

# Run (Ollama on this PC)
dotnet run --project src/InControl.App
```

## Prerequisiti

InControl richiede un backend LLM locale. Consigliamo [Ollama](https://ollama.ai/):

```bash
# Install Ollama from https://ollama.ai/download

# Pull a model
ollama pull llama3.2

# Start the server (runs on http://127.0.0.1:11434)
ollama serve
```

## Una GPU affittata

Impostazioni → **Dove viene eseguita questa chat**. Incolla il comando SSH diretto dal server affittato (`ssh -p <mapped-port> root@<public-ip> -i <key>`). Il pulsante indica che la chat verrà inviata a tale macchina.

L'inoltro ascolta sulla porta `127.0.0.1:11436` su questo PC, non sulla porta `11434`. La chat rimane qui finché Ollama non risponde tramite quella porta.

Lascia Ollama in esecuzione sulla porta `127.0.0.1:11434` sul server affittato. Non impostare la porta `OLLAMA_HOST=0.0.0.0` e non pubblicare la porta 11434. La porta SSH è la porta sshd mappata, non 11434.

Il proxy `ssh.runpod.io` di RunPod è solo uno shell. Non può inoltrare una porta. **Cerca i miei pod RunPod** usa `RUNPOD_API_KEY` dall'ambiente e compila il comando SSH sull'indirizzo IP pubblico del pod. La chiave non viene memorizzata. La ricerca non avvia un pod e non invia la chat. Vast documenta l'inoltro sull'indirizzo diretto. L'indirizzo smette di funzionare quando il server affittato viene riavviato. Cerca di nuovo il pod o incolla il nuovo comando. La chiave privata rimane su questo PC.

Le regole complete sono disponibili in [docs/COMPUTE.md](docs/COMPUTE.md).

## Compilazione

### Verifica dell'ambiente di compilazione

```powershell
# Run verification script
./scripts/verify.ps1
```

### Compilazione per lo sviluppo

```bash
dotnet build
```

### Compilazione per il rilascio

```powershell
# Creates release artifacts in artifacts/
./scripts/release.ps1
```

### Esecuzione dei test

```bash
dotnet test
```

## Architettura

InControl segue un'architettura pulita e a livelli:

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

Consulta [ARCHITECTURE.md](./docs/ARCHITECTURE.md) per la documentazione dettagliata del progetto.

### Sottosistemi chiave

| Sottosistema | Namespace | Scopo |
|-----------|-----------|---------|
| Assistente | `InControl.Core.Assistant` | Profili, archivio di memoria, protezione della personalità, onboarding |
| Plugin | `InControl.Core.Plugins` | Estensibilità convalidata tramite manifest e SDK, in un ambiente isolato |
| Policy | `InControl.Core.Policy` | Documenti policy JSON (organizzazione/team/utente), regole per strumenti/plugin/memoria/connettività |
| Connettività | `InControl.Core.Connectivity` | Governance di rete a tre modalità con traccia di controllo |
| Stato di salute | `InControl.Services.Health` | Controlli sullo stato di salute plug-in (app, inferenza, archiviazione) |
| Diagnostica | `InControl.Core.Diagnostics` | Creazione di un pacchetto di supporto con configurazioni sanificate |

## Archiviazione dati

Tutti i dati vengono archiviati localmente:

| Dati | Posizione |
|------|----------|
| Sessioni | `%LOCALAPPDATA%\InControl\sessions\` |
| Log | `%LOCALAPPDATA%\InControl\logs\` |
| Cache | `%LOCALAPPDATA%\InControl\cache\` |
| Esportazioni | `%USERPROFILE%\Documents\InControl\exports\` |

Consulta [PRIVACY.md](./docs/PRIVACY.md) per la documentazione completa sulla gestione dei dati.

## Risoluzione dei problemi

I problemi comuni e le relative soluzioni sono documentati in [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md).

### Soluzioni rapide

**L'app non si avvia:**
- Verifica che sia installato .NET 9.0 Runtime
- Esegui `dotnet --list-runtimes` per verificare

**Nessun modello disponibile:**
- Assicurati che Ollama sia in esecuzione: `ollama serve`
- Scarica un modello: `ollama pull llama3.2`

**GPU non rilevata:**
- Aggiorna i driver NVIDIA all'ultima versione
- Verifica l'installazione del toolkit CUDA

## Contributi

I contributi sono benvenuti! Si prega di:

1. Crea un fork del repository
2. Crea un branch per la funzionalità
3. Scrivi i test per la nuova funzionalità
4. Invia una pull request

## Segnalazione di problemi

1. Controlla prima [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md)
2. Utilizza la funzione "Copia diagnostica" nell'app
3. Apri un problema con le informazioni diagnostiche allegate

## Stack tecnologico

| Livello | Tecnologia |
|-------|------------|
| Framework UI | WinUI 3 (Windows App SDK 1.6) |
| Architettura | MVVM con CommunityToolkit.Mvvm |
| Integrazione LLM | OllamaSharp, Microsoft.Extensions.AI |
| Contenitore DI | Microsoft.Extensions.DependencyInjection |
| Configurazione | Microsoft.Extensions.Configuration |
| Registrazione eventi | Microsoft.Extensions.Logging + Serilog |

## Version

Current version: **2.0.0**. The package identity is `InControl.App` at `2.0.0.0`, because Partner Center already has this app through 1.4.0. The name on the repo stays InControl.

See [CHANGELOG.md](./CHANGELOG.md) for why the version jumped, and for the 0.3.0 history.

## Sicurezza e ambito dei dati

InControl è un'applicazione di chat WinUI 3 per Ollama.

- **Dati accessibili:** Ollama su questo PC, cronologia delle chat nell'archiviazione locale e, solo dopo averne connesso uno, Ollama su una macchina accessibile tramite SSH.
- **Dati non accessibili:** Nessun account InControl, nessun dato di telemetria, nessuna analisi.
- **Autorizzazioni:** Accesso HTTP in loopback a Ollama, un client SSH su questo PC quando si connette una macchina remota e il file system per la cronologia delle chat.

Politica completa: [SECURITY.md](SECURITY.md)

---

## Supporto

- **Segnalazioni di bug:** [Issues](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **Sicurezza:** [SECURITY.md](SECURITY.md)

## Licenza

[MIT](LICENSE) – per il testo completo, consultare [LICENSE](LICENSE).

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
