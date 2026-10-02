<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.md">English</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
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

**Ollama pour le chat sur Windows.** Par défaut, sur cet ordinateur. Une GPU que vous avez louée, via SSH, si vous le souhaitez.

InControl utilise l’API HTTP d’Ollama. Rien n’est envoyé à une instance louée tant que vous n’en avez pas connecté une. La barre en haut affiche alors le nom de cette machine et indique que le chat quitte cet ordinateur.

## Pourquoi InControl ?

- **Cet ordinateur en premier.** Les requêtes restent ici jusqu’à ce que vous connectiez une GPU louée.
- **Le même Ollama, quelle que soit la configuration.** Une instance louée exécute Ollama sur `127.0.0.1:11434`. InControl y accède via un transfert local SSH. Le client HTTP ne communique jamais avec un port public.
- **Pas de liste blanche d’outils.** La connectivité contrôle les URL des outils d’assistance. Elle ne décide pas de l’endroit où le chat s’exécute.
- **WinUI 3.** Une application Windows, avec du markdown dans le fil de discussion.
- **Profils d’assistance** : personnalité configurable, niveau de détail et tolérance au risque.
- **Système de plugins** : étendez les fonctionnalités avec des plugins isolés et un SDK basé sur un manifeste.
- **Moteur de stratégie** : les couches de stratégie d’organisation/équipe/utilisateur régissent les outils, les plugins, la mémoire et la connectivité.
- **Modes de connectivité** : uniquement hors ligne, assisté ou connecté avec un enregistrement complet.

## Packages NuGet

Les bibliothèques principales sont disponibles sous forme de packages NuGet autonomes pour créer vos propres intégrations d’IA locales :

| Package | Version | Description |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | Modèles de domaine, types de conversation et abstractions partagées pour les applications de chat IA locales. |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | Couche d’abstraction du backend LLM avec chat en streaming, gestion des modèles et vérifications de l’état. Inclut l’implémentation d’Ollama. |

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

## Matériel cible

| Composant | Minimum | Recommandé |
|-----------|---------|-------------|
| GPU | RTX 3060 (8 Go) | RTX 4080/5080 (16 Go) |
| RAM | 16 Go | 32 Go |
| OS | Windows 10 1809+ | Windows 11 |
| .NET | 9.0 | 9.0 |

## Installation

Compilation à partir du code source. Il n’existe pas encore de version MSIX de ce dépôt.

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

Paramètres → **Où ce chat s’exécute**. Collez la commande SSH directe de l’instance louée (`ssh -p <mapped-port> root@<public-ip> -i <key>`). Le bouton indique que le chat sera envoyé à cette machine.

Le transfert écoute sur `127.0.0.1:11436` sur cet ordinateur, et non sur `11434`. Le chat reste ici jusqu’à ce qu’Ollama réponde via ce port.

Laissez Ollama sur `127.0.0.1:11434` sur l’instance louée. Ne définissez pas `OLLAMA_HOST=0.0.0.0` et ne publiez pas le port 11434. Le port SSH est le port sshd mappé, et non 11434.

Le proxy `ssh.runpod.io` de RunPod est uniquement une coquille. Il ne peut pas transférer un port. « Rechercher mes pods RunPod » utilise `RUNPOD_API_KEY` de l’environnement et remplit l’adresse IP publique directe du pod. La clé n’est pas stockée. La recherche ne démarre pas un pod et n’envoie pas le chat. Vast documente le transfert sur l’adresse directe. L’adresse cesse de fonctionner lorsque l’instance louée redémarre. Recherchez à nouveau le pod ou collez la nouvelle commande. La clé privée reste sur cet ordinateur.

Les règles complètes sont disponibles dans [docs/COMPUTE.md](docs/COMPUTE.md).

## Compilation

### Vérification de l’environnement de compilation

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

### Exécution des tests

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

Consultez [ARCHITECTURE.md](./docs/ARCHITECTURE.md) pour obtenir une documentation détaillée sur la conception.

### Sous-systèmes clés

| Sous-système | Espace de noms | Objectif |
|-----------|-----------|---------|
| Assistant | `InControl.Core.Assistant` | Profils, stockage de la mémoire, protection de la personnalité, intégration |
| Plugins | `InControl.Core.Plugins` | Extensibilité validée par un manifeste et isolée avec un SDK |
| Stratégie | `InControl.Core.Policy` | Documents de stratégie JSON (organisation/équipe/utilisateur), règles pour les outils/plugins/mémoire/connectivité |
| Connectivité | `InControl.Core.Connectivity` | Gouvernance du réseau en trois modes avec un historique d’audit |
| État | `InControl.Services.Health` | Vérifications de l’état modulaires (application, inférence, stockage) |
| Diagnostics | `InControl.Core.Diagnostics` | Création d’un ensemble de support avec des configurations anonymisées |

## Stockage des données

Toutes les données sont stockées localement :

| Données | Emplacement |
|------|----------|
| Sessions | `%LOCALAPPDATA%\InControl\sessions\` |
| Journaux | `%LOCALAPPDATA%\InControl\logs\` |
| Cache | `%LOCALAPPDATA%\InControl\cache\` |
| Exportations | `%USERPROFILE%\Documents\InControl\exports\` |

Consultez [PRIVACY.md](./docs/PRIVACY.md) pour obtenir une documentation complète sur la gestion des données.

## Résolution des problèmes

Les problèmes courants et leurs solutions sont documentés dans [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md).

### Corrections rapides

**L’application ne démarre pas :**
- Vérifiez que .NET 9.0 Runtime est installé.
- Exécutez `dotnet --list-runtimes` pour vérifier.

**Aucun modèle disponible :**
- Assurez-vous qu’Ollama est en cours d’exécution : `ollama serve`.
- Téléchargez un modèle : `ollama pull llama3.2`.

**GPU non détectée :**
- Mettez à jour les pilotes NVIDIA vers la dernière version.
- Vérifiez l’installation du kit d’outils CUDA.

## Contribution

Les contributions sont les bienvenues ! Veuillez :

1. Créer une branche du dépôt.
2. Créer une branche de fonctionnalité.
3. Écrire des tests pour les nouvelles fonctionnalités.
4. Soumettre une demande de tirage.

## Signalement des problèmes

1. Vérifiez d’abord [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md).
2. Utilisez la fonction « Copier les diagnostics » dans l’application.
3. Ouvrez un problème en joignant les informations de diagnostic.

## Pile technologique

| Couche | Technologie |
|-------|------------|
| Framework d’interface utilisateur | WinUI 3 (Windows App SDK 1.6) |
| Architecture | MVVM avec CommunityToolkit.Mvvm |
| Intégration LLM | OllamaSharp, Microsoft.Extensions.AI |
| Conteneur DI | Microsoft.Extensions.DependencyInjection |
| Configuration | Microsoft.Extensions.Configuration |
| Journalisation | Microsoft.Extensions.Logging + Serilog |

## Version

Version actuelle : **0.3.0**

Consultez le fichier [CHANGELOG.md](./CHANGELOG.md) pour connaître l’historique des versions.

## Sécurité et portée des données

InControl est une application de chat WinUI 3 pour Ollama.

- **Données auxquelles on accède :** Ollama sur cet ordinateur, historique des conversations dans le stockage local et, uniquement après la connexion, Ollama sur une machine accessible via SSH.
- **Données auxquelles on n’accède pas :** Aucun compte InControl, aucune télémétrie, aucune analyse.
- **Autorisations :** Accès HTTP en boucle à Ollama, un client SSH sur cet ordinateur lorsque vous connectez une machine distante et le système de fichiers pour l’historique des conversations.

Politique complète : [SECURITY.md](SECURITY.md)

---

## Assistance

- **Signalement de bogues :** [Issues](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **Sécurité :** [SECURITY.md](SECURITY.md)

## Licence

[MIT](LICENSE) – consultez le fichier [LICENSE](LICENSE) pour le texte intégral.

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
