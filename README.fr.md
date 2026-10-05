<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.md">English</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
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

**Ollama pour Windows.** Par défaut, sur cet ordinateur. Une GPU que vous avez louée, via SSH, si vous le souhaitez.

InControl utilise l’API HTTP d’Ollama. Rien n’est envoyé à une machine louée tant que vous n’en avez pas connecté une. La barre en haut affiche ensuite le nom de cette machine et indique que la conversation se déroule sur cet ordinateur.

## Pourquoi InControl ?

- **Cet ordinateur en premier.** Les requêtes restent ici jusqu’à ce que vous connectiez une GPU louée.
- **Le même Ollama, quelle que soit la configuration.** Une machine louée exécute Ollama sur `127.0.0.1:11434`. InControl y accède via un transfert local SSH. Le client HTTP ne communique jamais avec un port public.
- **Pas de liste blanche d’outils.** La connectivité contrôle les URL des outils d’assistance. Elle ne décide pas de l’endroit où la conversation se déroule.
- **Projets et notes.** Enregistrez les sessions dans des projets avec leurs propres instructions. Demandez-lui de se souvenir d’une note pour un projet ou une session, et les notes correspondantes seront incluses dans le message suivant.
- **Recherche sur le Web, si vous l’activez.** Le bouton Web permet à un modèle capable d’utiliser des outils de rechercher sur DuckDuckGo et de lire des pages publiques. Il est désactivé jusqu’à ce que vous l’activiez, et chaque réponse affiche les recherches effectuées.
- **Joindre des fichiers.** Ajoutez des fichiers texte et de code à un message, ou des images pour un modèle de vision tel que gemma3 ou llama3.2-vision. Utilisez l’icône punaise, Ctrl+Maj+O, ou faites glisser des fichiers dans la zone de composition.
- **Voix sur cet ordinateur.** Les réponses peuvent être lues à voix haute par Kokoro, qui s’exécute sur cet ordinateur. Le modèle vocal est téléchargé une seule fois, la première fois que la voix est nécessaire : lorsqu’une réponse est prononcée, ou lorsque vous ouvrez les paramètres.
- **WinUI 3.** Une application Windows native. Les réponses rendent le texte en Markdown : emphase, blocs de code, listes, tableaux et liens.
- **Moteur de stratégie.** Les documents de stratégie d’organisation, d’équipe et d’utilisateur régissent les outils, la mémoire et la connectivité.
- **Modes de connectivité.** Uniquement hors ligne, assisté ou connecté, avec un journal d’audit.

## Packages NuGet

Les bibliothèques principales sont disponibles sous forme de packages NuGet autonomes pour créer vos propres intégrations d’IA locales :

| Package | Version | Description |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | Modèles de domaine, types de conversation et abstractions partagées pour les applications de chat IA locales. |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | Couche d’abstraction du backend LLM avec chat en streaming, gestion des modèles et vérifications de l’état. Inclut l’implémentation Ollama. |

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

## Matériel cible

| Composant | Minimum | Recommandé |
|-----------|---------|-------------|
| GPU | RTX 3060 (8 Go) | RTX 4080/5080 (16 Go) |
| RAM | 16 Go | 32 Go |
| OS | Windows 10, version 2004 (x64) | Windows 11 |
| .NET | 9.0 | 9.0 |

## Installation

**Microsoft Store :** [InControl-Desktop](https://apps.microsoft.com/detail/9N1FG39JWF83). Le package du Store contient ses propres environnements d’exécution .NET et Windows App SDK, et le Store assure sa mise à jour.

**Téléchargement portable :** `InControl-<version>-win-x64-portable.zip` sur [GitHub Releases](https://github.com/mcp-tool-shop-org/incontrol/releases/latest). Décompressez-le n’importe où et exécutez `InControl.App.exe` ; les environnements d’exécution sont inclus, donc rien n’est installé. Il n’est pas signé numériquement, Windows peut donc vous demander de confirmer la première exécution. Un fichier `.sha256` se trouve à côté.

**À partir du code source :**

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build

# Run (Ollama on this PC)
dotnet run --project src/InControl.App
```

## Prérequis

InControl nécessite un backend LLM local. Nous recommandons [Ollama](https://ollama.ai/) :

```bash
# Install Ollama from https://ollama.ai/download

# Pull a model
ollama pull llama3.2

# Start the server (runs on http://127.0.0.1:11434)
ollama serve
```

## Une GPU louée

Paramètres → **Où cette conversation s’exécute**. Collez la commande SSH directe de la machine louée (`ssh -p <mapped-port> root@<public-ip> -i <key>`). Le bouton indique que la conversation sera envoyée à cette machine.

Le transfert écoute sur `127.0.0.1:11436` sur cet ordinateur, et non sur `11434`. La conversation reste ici jusqu’à ce qu’Ollama réponde via ce port.

Laissez Ollama actif sur `127.0.0.1:11434` sur la machine louée. Ne définissez pas `OLLAMA_HOST=0.0.0.0` et ne publiez pas le port 11434. Le port SSH est le port sshd mappé, et non 11434.

Le proxy `ssh.runpod.io` de RunPod est uniquement une coquille. Il ne peut pas transférer un port. **Rechercher mes pods RunPod** utilise `RUNPOD_API_KEY` de l’environnement et remplit l’adresse SSH publique directe du pod. La clé n’est pas stockée. La recherche ne démarre pas un pod et n’envoie pas la conversation. Vast documente le transfert sur l’adresse directe. L’adresse expire lorsque la machine louée redémarre. Recherchez à nouveau le pod ou collez la nouvelle commande. La clé privée reste sur cet ordinateur.

Les règles complètes se trouvent dans [docs/COMPUTE.md](docs/COMPUTE.md).

## Compilation

### Vérifier l’environnement de compilation

```powershell
# Run verification script
./scripts/verify.ps1
```

### Compilation pour le développement

```bash
dotnet build
```

### Compilation pour la publication

```powershell
# Creates release artifacts in artifacts/
./scripts/release.ps1
```

### Exécuter les tests

```bash
dotnet test
```

## Architecture

InControl suit une architecture propre et structurée :

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

Consultez [ARCHITECTURE.md](./docs/ARCHITECTURE.md) pour une documentation détaillée de la conception.

### Sous-systèmes clés

| Sous-système | Espace de noms | Objectif |
|-----------|-----------|---------|
| Assistant | `InControl.Core.Assistant` | Profils, stockage de la mémoire, protection de la personnalité, intégration |
| Plugins | `InControl.Core.Plugins` | Extensibilité validée par un manifeste et sécurisée avec un SDK |
| Stratégie | `InControl.Core.Policy` | Documents de stratégie JSON (organisation/équipe/utilisateur), règles pour les outils/plugins/mémoire/connectivité |
| Connectivité | `InControl.Core.Connectivity` | Gouvernance du réseau en trois modes avec un journal d’audit |
| État | `InControl.Services.Health` | Vérifications de l’état modulaires (application, inférence, stockage) |
| Diagnostics | `InControl.Core.Diagnostics` | Création d’un ensemble de support avec des configurations anonymisées |

## Stockage des données

Toutes les données sont stockées localement :

| Données | Emplacement |
|------|----------|
| Sessions | `%LOCALAPPDATA%\InControl\sessions\` |
| Projets et notes | `%LOCALAPPDATA%\InControl\` |
| Journaux | `%LOCALAPPDATA%\InControl\logs\` |
| Cache (modèle vocal) | `%LOCALAPPDATA%\InControl\cache\` |
| Exportations | `%USERPROFILE%\Documents\InControl\exports\` |

Une installation du Store peut conserver ce dossier dans le stockage propre de l’application, que Windows supprime lors de la désinstallation.

La politique de confidentialité est disponible à l’adresse [PRIVACY.md](./PRIVACY.md). Les détails concernant le traitement des données se trouvent dans [docs/PRIVACY.md](./docs/PRIVACY.md).

## Résolution des problèmes

Les problèmes courants et leurs solutions sont documentés dans [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md).

### Corrections rapides

**L’application ne démarre pas (construction à partir du code source) :**
- Vérifiez que le SDK .NET 9 est installé : `dotnet --list-sdks`
- Le package du Store n’a pas besoin d’un environnement d’exécution distinct

**Aucun modèle disponible :**
- Assurez-vous qu’Ollama est en cours d’exécution : `ollama serve`
- Téléchargez un modèle : `ollama pull llama3.2`

**Les réponses sont lentes :**
- Ollama décide si le modèle s’exécute sur le GPU. `ollama ps` indique la quantité de données qui s’exécute sur le GPU
- Mettez à jour le pilote du GPU ou téléchargez un modèle plus petit

## Contribution

Les contributions sont les bienvenues ! Veuillez :

1. Créer une branche du dépôt
2. Créer une branche de fonctionnalité
3. Écrire des tests pour les nouvelles fonctionnalités
4. Soumettre une demande de fusion

## Signalement des problèmes

1. Vérifiez d’abord [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md)
2. Utilisez la fonction « Copier les diagnostics » dans l’application
3. Ouvrez un problème en joignant les informations de diagnostic

## Pile technologique

| Couche | Technologie |
|-------|------------|
| Framework d’interface utilisateur | WinUI 3 (Windows App SDK 1.6) |
| Architecture | MVVM avec CommunityToolkit.Mvvm |
| Intégration LLM | OllamaSharp, Microsoft.Extensions.AI |
| Conteneur DI | Microsoft.Extensions.DependencyInjection |
| Configuration | Microsoft.Extensions.Configuration |
| Journalisation | Microsoft.Extensions.Logging + Serilog |

## Version

Version actuelle : **2.0.1**. Dans le Microsoft Store, il s’agit de InControl-Desktop, l’identité du package est `mcp-tool-shop.InControl-Desktop`, la version est `2.0.1.0`. Le Store proposait déjà cette application en version 1.4.0, de sorte que la version du package commence au-dessus de cette valeur. Le nom du dépôt, de la fenêtre et de la vignette du menu Démarrer est InControl.

Consultez [CHANGELOG.md](./CHANGELOG.md) pour savoir pourquoi la version a augmenté et pour consulter l’historique de la version 0.3.0.

## Sécurité et portée des données

InControl est une application de chat WinUI 3 pour Ollama.

- **Données auxquelles on accède :** Ollama sur cet ordinateur, historique du chat, projets et notes dans le stockage local et, uniquement après que vous en ayez connecté un, Ollama sur une machine à laquelle vous accédez via SSH
- **Données auxquelles on n’accède pas :** Aucun compte InControl, aucune télémétrie, aucune analyse
- **Autorisations :** HTTP en boucle vers Ollama, un client SSH sur cet ordinateur lorsque vous connectez une machine virtuelle et le système de fichiers pour l’historique du chat

Politique complète : [SECURITY.md](SECURITY.md)

---

## Assistance

- **Signalement des bogues :** [Issues](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **Sécurité :** [SECURITY.md](SECURITY.md)

## Licence

[MIT](LICENSE) – consultez [LICENSE](LICENSE) pour le texte intégral.

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
