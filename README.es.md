<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.md">English</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
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

**Ollama chat para Windows.** En este PC, por defecto. Una GPU que alquilaste, a través de SSH, cuando lo indiques.

InControl utiliza la API HTTP de Ollama. Nada se envía a un servidor alquilado hasta que conectes uno. La barra en la parte superior luego identifica esa máquina y dice que el chat sale de este PC.

## ¿Por qué InControl?

- **Este PC primero.** Los mensajes permanecen aquí hasta que conectes una GPU alquilada.
- **El mismo Ollama en ambos casos.** Un servidor alquilado ejecuta Ollama en `127.0.0.1:11434`. InControl se conecta a él mediante un reenvío local SSH. El cliente HTTP nunca se comunica con un puerto público.
- **No está en la lista de herramientas permitidas.** La conectividad controla las URL de las herramientas de asistencia. No decide dónde se ejecuta el chat.
- **WinUI 3.** Una aplicación de Windows, con formato markdown en el hilo de conversación.
- **Perfiles de asistente:** Personalidad, nivel de detalle y tolerancia al riesgo configurables.
- **Sistema de complementos:** Amplía la funcionalidad con complementos aislados y un SDK basado en manifiestos.
- **Motor de políticas:** Las capas de políticas de organización/equipo/usuario rigen las herramientas, los complementos, la memoria y la conectividad.
- **Modos de conectividad:** Solo sin conexión, asistido o conectado con registro de auditoría completo.

## Paquetes NuGet

Las bibliotecas principales están disponibles como paquetes NuGet independientes para crear tus propias integraciones de IA locales:

| Paquete | Versión | Descripción |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | Modelos de dominio, tipos de conversación y abstracciones compartidas para aplicaciones de chat de IA locales. |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | Capa de abstracción del backend LLM con chat en streaming, administración de modelos y comprobaciones de estado. Incluye la implementación de Ollama. |

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
| OS | Windows 10 1809+ | Windows 11 |
| .NET | 9.0 | 9.0 |

## Instalación

Compilar desde el código fuente. Aún no hay una versión MSIX de este repositorio.

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

InControl sigue una arquitectura limpia y por capas:

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
| Asistente | `InControl.Core.Assistant` | Perfiles, almacenamiento de memoria, protección de la personalidad, incorporación |
| Complementos | `InControl.Core.Plugins` | Extensibilidad validada por manifiesto y aislada con SDK |
| Política | `InControl.Core.Policy` | Documentos de política JSON (organización/equipo/usuario), reglas de herramientas/complementos/memoria/conectividad |
| Conectividad | `InControl.Core.Connectivity` | Gobernanza de red de tres modos con registro de auditoría |
| Estado | `InControl.Services.Health` | Comprobaciones de estado conectables (aplicación, inferencia, almacenamiento) |
| Diagnóstico | `InControl.Core.Diagnostics` | Creación de paquetes de soporte con configuraciones sanitizadas |

## Almacenamiento de datos

Todos los datos se almacenan localmente:

| Datos | Ubicación |
|------|----------|
| Sesiones | `%LOCALAPPDATA%\InControl\sessions\` |
| Registros | `%LOCALAPPDATA%\InControl\logs\` |
| Caché | `%LOCALAPPDATA%\InControl\cache\` |
| Exportaciones | `%USERPROFILE%\Documents\InControl\exports\` |

Consulta [PRIVACY.md](./docs/PRIVACY.md) para obtener documentación completa sobre el manejo de datos.

## Solución de problemas

Los problemas comunes y sus soluciones se documentan en [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md).

### Soluciones rápidas

**La aplicación no se inicia:**
- Verifica que esté instalado .NET 9.0 Runtime
- Ejecuta `dotnet --list-runtimes` para verificar

**No hay modelos disponibles:**
- Asegúrate de que Ollama se esté ejecutando: `ollama serve`
- Descarga un modelo: `ollama pull llama3.2`

**No se detecta la GPU:**
- Actualiza los controladores de NVIDIA a la última versión
- Verifica la instalación del kit de herramientas CUDA

## Contribución

¡Las contribuciones son bienvenidas! Por favor:

1. Haz un fork del repositorio
2. Crea una rama de características
3. Escribe pruebas para la nueva funcionalidad
4. Envía una solicitud de extracción

## Informar de problemas

1. Primero, consulta [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md)
2. Utiliza la función "Copiar diagnóstico" en la aplicación
3. Abre un problema con la información de diagnóstico adjunta

## Pila tecnológica

| Capa | Tecnología |
|-------|------------|
| Marco de la interfaz de usuario | WinUI 3 (Windows App SDK 1.6) |
| Arquitectura | MVVM con CommunityToolkit.Mvvm |
| Integración de LLM | OllamaSharp, Microsoft.Extensions.AI |
| Contenedor de DI | Microsoft.Extensions.DependencyInjection |
| Configuración | Microsoft.Extensions.Configuration |
| Registro | Microsoft.Extensions.Logging + Serilog |

## Version

Current version: **2.0.0**. The package identity is `InControl.App` at `2.0.0.0`, because Partner Center already has this app through 1.4.0. The name on the repo stays InControl.

See [CHANGELOG.md](./CHANGELOG.md) for why the version jumped, and for the 0.3.0 history.

## Seguridad y ámbito de los datos

InControl es una aplicación de chat WinUI 3 para Ollama.

- **Datos a los que se accede:** Ollama en este PC, historial de chat en el almacenamiento local y, solo después de que se conecte uno, Ollama en una máquina a la que se accede a través de SSH.
- **Datos a los que no se accede:** Ninguna cuenta de InControl, ningún telemetría, ningún análisis.
- **Permisos:** HTTP de bucle de retorno a Ollama, un cliente SSH en este PC cuando se conecta una máquina remota y el sistema de archivos para el historial de chat.

Política completa: [SECURITY.md](SECURITY.md)

---

## Soporte

- **Informes de errores:** [Issues](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **Seguridad:** [SECURITY.md](SECURITY.md)

## Licencia

[MIT](LICENSE) — consulte [LICENSE](LICENSE) para ver el texto completo.

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
