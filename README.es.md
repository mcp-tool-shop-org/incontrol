<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.md">English</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
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

**Ollama chat para Windows.** En este PC, por defecto. Una GPU que alquilaste, a través de SSH, cuando lo indiques.

InControl utiliza la API HTTP de Ollama. Nada se envía a un servidor alquilado hasta que conectes uno. La barra en la parte superior muestra el nombre de esa máquina e indica que el chat se ejecuta en este PC.

## ¿Por qué InControl?

- **Este PC primero.** Los mensajes permanecen aquí hasta que conectes una GPU alquilada.
- **La misma versión de Ollama en ambos casos.** Un servidor alquilado ejecuta Ollama en `127.0.0.1:11434`. InControl se conecta a él mediante un reenvío local SSH. El cliente HTTP nunca se comunica con un puerto público.
- **No está en la lista de herramientas permitidas.** La conectividad controla las URL de las herramientas de asistencia. No decide dónde se ejecuta el chat.
- **Proyectos y notas.** Guarda las sesiones en proyectos con sus propias instrucciones. Pídele que recuerde una nota para un proyecto o una sesión, y las notas correspondientes se incluirán en el siguiente mensaje.
- **Búsqueda web, cuando la actives.** El botón Web permite que un modelo que puede usar herramientas busque en DuckDuckGo y lea páginas públicas. Está desactivado hasta que lo actives, y cada respuesta muestra las búsquedas que realizó.
- **Adjuntar archivos.** Agrega archivos de texto y código a un mensaje, o imágenes para un modelo de visión como gemma3 o llama3.2-vision. Usa el clip, Ctrl+Shift+O o arrastra los archivos al cuadro de composición.
- **Voz en este PC.** Las respuestas pueden ser leídas en voz alta por Kokoro, que se ejecuta en este PC. El modelo de voz se descarga una vez, la primera vez que se necesita la voz: cuando se lee una respuesta o cuando abres la Configuración.
- **WinUI 3.** Una aplicación nativa de Windows. Las respuestas se muestran en formato markdown: énfasis, bloques de código, listas, tablas y enlaces.
- **Motor de políticas.** Los documentos de políticas de la organización, el equipo y el usuario rigen las herramientas, la memoria y la conectividad.
- **Modos de conectividad.** Solo sin conexión, asistido o conectado, con un registro de auditoría.

## Paquetes NuGet

Las bibliotecas principales están disponibles como paquetes NuGet independientes para crear tus propias integraciones de IA locales:

| Paquete | Versión | Descripción |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | Modelos de dominio, tipos de conversación y abstracciones compartidas para aplicaciones de chat de IA locales. |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | Capa de abstracción de backend LLM con chat en streaming, administración de modelos y comprobaciones de estado. Incluye la implementación de Ollama. |

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
| GPU | RTX 3060 (8 GB) | RTX 4080/5080 (16 GB) |
| RAM | 16 GB | 32 GB |
| OS | Windows 10 versión 2004 (x64) | Windows 11 |
| .NET | 9.0 | 9.0 |

## Instalación

**Microsoft Store:** [InControl-Desktop](https://apps.microsoft.com/detail/9N1FG39JWF83). El paquete de la tienda incluye sus propios entornos de ejecución de .NET y Windows App SDK, y la tienda se encarga de mantenerlo actualizado.

**Descarga portátil:** `InControl-<version>-win-x64-portable.zip` en [GitHub Releases](https://github.com/mcp-tool-shop-org/incontrol/releases/latest). Descomprímelo en cualquier lugar y ejecuta `InControl.App.exe`; los entornos de ejecución están dentro, por lo que no se instala nada. No está firmado digitalmente, por lo que Windows puede pedirte que confirmes la primera ejecución. Un archivo `.sha256` se encuentra junto a él.

**Desde el código fuente:**

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build

# Run (Ollama on this PC)
dotnet run --project src/InControl.App
```

## Requisitos previos

InControl requiere un backend LLM local. Recomendamos [Ollama](https://ollama.ai/):

```bash
# Install Ollama from https://ollama.ai/download

# Pull a model
ollama pull llama3.2

# Start the server (runs on http://127.0.0.1:11434)
ollama serve
```

## Una GPU alquilada

Configuración → **Dónde se ejecuta este chat**. Pega el comando SSH directo del servidor alquilado (`ssh -p <mapped-port> root@<public-ip> -i <key>`). El botón indica que el chat se enviará a esa máquina.

El reenvío escucha en `127.0.0.1:11436` en este PC, no en `11434`. El chat permanece aquí hasta que Ollama responda a través de ese puerto.

Deja Ollama en `127.0.0.1:11434` en el servidor alquilado. No configures `OLLAMA_HOST=0.0.0.0` y no publiques el puerto 11434. El puerto SSH es el puerto sshd asignado, no 11434.

El proxy `ssh.runpod.io` de RunPod es solo una shell. No puede reenviar un puerto. **Buscar mis pods de RunPod** utiliza `RUNPOD_API_KEY` del entorno y completa la dirección IP pública directa del pod. La clave no se almacena. La búsqueda no inicia un pod ni envía el chat. Vast documenta el reenvío en la dirección directa. La dirección deja de funcionar cuando el servidor alquilado se reinicia. Busca el pod de nuevo o pega el nuevo comando. La clave privada permanece en este PC.

Las reglas completas están en [docs/COMPUTE.md](docs/COMPUTE.md).

## Compilación

### Verificar el entorno de compilación

```powershell
# Run verification script
./scripts/verify.ps1
```

### Compilación de desarrollo

```bash
dotnet build
```

### Compilación de lanzamiento

```powershell
# Creates release artifacts in artifacts/
./scripts/release.ps1
```

### Ejecutar pruebas

```bash
dotnet test
```

## Arquitectura

InControl sigue una arquitectura limpia y en capas:

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

Consulta [ARCHITECTURE.md](./docs/ARCHITECTURE.md) para obtener documentación detallada del diseño.

### Subsistemas clave

| Subsistema | Espacio de nombres | Propósito |
|-----------|-----------|---------|
| Asistente | `InControl.Core.Assistant` | Perfiles, almacén de memoria, protección de la personalidad, incorporación |
| Plugins | `InControl.Core.Plugins` | Extensibilidad con SDK validada por manifiesto y en un entorno aislado |
| Política | `InControl.Core.Policy` | Documentos de política JSON (organización/equipo/usuario), reglas de herramientas/plugins/memoria/conectividad |
| Conectividad | `InControl.Core.Connectivity` | Gobernanza de red de tres modos con registro de auditoría |
| Estado | `InControl.Services.Health` | Comprobaciones de estado conectables (aplicación, inferencia, almacenamiento) |
| Diagnóstico | `InControl.Core.Diagnostics` | Creación de paquetes de soporte con configuraciones sanitizadas |

## Almacenamiento de datos

Todos los datos se almacenan localmente:

| Datos | Ubicación |
|------|----------|
| Sesiones | `%LOCALAPPDATA%\InControl\sessions\` |
| Proyectos y notas | `%LOCALAPPDATA%\InControl\` |
| Registros | `%LOCALAPPDATA%\InControl\logs\` |
| Caché (modelo de voz) | `%LOCALAPPDATA%\InControl\cache\` |
| Exportaciones | `%USERPROFILE%\Documents\InControl\exports\` |

Una instalación de la tienda puede mantener esa carpeta dentro del propio almacenamiento de la aplicación, que Windows elimina al desinstalarla.

La política de privacidad es [PRIVACY.md](./PRIVACY.md). Los detalles sobre el manejo de datos se encuentran en [docs/PRIVACY.md](./docs/PRIVACY.md).

## Solución de problemas

Los problemas comunes y sus soluciones se documentan en [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md).

### Soluciones rápidas

**La aplicación no se inicia (compilación desde el código fuente):**
- Compruebe que el SDK de .NET 9 esté instalado: `dotnet --list-sdks`
- El paquete de la tienda no necesita un entorno de ejecución independiente

**No hay modelos disponibles:**
- Asegúrese de que Ollama esté en ejecución: `ollama serve`
- Descargue un modelo: `ollama pull llama3.2`

**Las respuestas son lentas:**
- Ollama decide si el modelo se ejecuta en la GPU. `ollama ps` muestra cuánta parte de él se ejecuta en la GPU
- Actualice el controlador de la GPU o descargue un modelo más pequeño

## Contribuciones

¡Las contribuciones son bienvenidas! Por favor:

1. Haga una bifurcación del repositorio
2. Cree una rama de características
3. Escriba pruebas para la nueva funcionalidad
4. Envíe una solicitud de incorporación de cambios

## Informar de problemas

1. Primero, consulte [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md)
2. Utilice la función "Copiar diagnósticos" en la aplicación
3. Abra un problema con la información de diagnóstico adjunta

## Pila tecnológica

| Capa | Tecnología |
|-------|------------|
| Marco de interfaz de usuario | WinUI 3 (Windows App SDK 1.6) |
| Arquitectura | MVVM con CommunityToolkit.Mvvm |
| Integración de LLM | OllamaSharp, Microsoft.Extensions.AI |
| Contenedor de DI | Microsoft.Extensions.DependencyInjection |
| Configuración | Microsoft.Extensions.Configuration |
| Registro | Microsoft.Extensions.Logging + Serilog |

## Versión

Versión actual: **2.0.1**. En la Microsoft Store, se llama InControl-Desktop, la identidad del paquete es `mcp-tool-shop.InControl-Desktop` y la versión es `2.0.1.0`. La tienda ya tenía esta aplicación en la versión 1.4.0, por lo que la versión del paquete comienza por encima de esa. El nombre en el repositorio, en la ventana y en el icono del menú Inicio es InControl.

Consulte [CHANGELOG.md](./CHANGELOG.md) para saber por qué la versión cambió y para ver el historial de la versión 0.3.0.

## Seguridad y alcance de los datos

InControl es una aplicación de chat WinUI 3 para Ollama.

- **Datos a los que se accede:** Ollama en este PC, historial de chat, proyectos y notas en el almacenamiento local y, solo después de que se conecte uno, Ollama en una máquina a la que se accede a través de SSH
- **Datos a los que no se accede:** Ninguna cuenta de InControl, ningún telemetría, ningún análisis
- **Permisos:** HTTP de bucle de retorno a Ollama, un cliente SSH en este PC cuando se conecta un servidor y el sistema de archivos para el historial de chat

Política completa: [SECURITY.md](SECURITY.md)

---

## Soporte

- **Informes de errores:** [Issues](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **Seguridad:** [SECURITY.md](SECURITY.md)

## Licencia

[MIT](LICENSE) -- consulte [LICENSE](LICENSE) para ver el texto completo.

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
