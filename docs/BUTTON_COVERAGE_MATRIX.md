# Button Coverage Matrix

> Complete coverage of all interactive UI elements in InControl.

## Coverage Status

| Section | Controls | Tested | Status |
|---------|----------|--------|--------|
| AppBar | 8 | 8 | ✅ 100% |
| StatusStrip | 6 | 6 | ✅ 100% |
| Settings Page | 12 | 12 | ✅ 100% |
| Model Manager | 10 | 10 | ✅ 100% |
| Help Page | 4 | 4 | ✅ 100% |
| Command Palette | 10 | 10 | ✅ 100% |
| Session Sidebar | 4 | 4 | ✅ 100% |
| Input Composer | 4 | 4 | ✅ 100% |
| **Total** | **58** | **58** | ✅ **100%** |

---

## AppBar Controls

| Control | Expected Result | Test Name |
|---------|-----------------|-----------|
| Settings Button | Opens Settings page | `AppBar_SettingsButton_OpensSettings` |
| Model Manager Button | Opens Model Manager | `AppBar_ModelManagerButton_OpensModelManager` |
| Assistant Button | Opens Assistant page | `AppBar_AssistantButton_OpensAssistant` |
| Extensions Button | Opens Extensions page | `AppBar_ExtensionsButton_OpensExtensions` |
| Policy Button | Opens Policy page | `AppBar_PolicyButton_OpensPolicy` |
| Connectivity Button | Opens Connectivity page | `AppBar_ConnectivityButton_OpensConnectivity` |
| Help Button | Opens Help page | `AppBar_HelpButton_OpensHelp` |
| Command Palette Button | Opens Command Palette | `AppBar_CommandPaletteButton_ShowsOverlay` |

## StatusStrip Controls

| Control | Expected Result | Test Name |
|---------|-----------------|-----------|
| Model Status | Opens Model Manager | `StatusStrip_ModelStatus_OpensModelManager` |
| Device Status | Opens Model Manager | `StatusStrip_DeviceStatus_OpensModelManager` |
| Connectivity Status | Opens Connectivity page | `StatusStrip_ConnectivityStatus_OpensConnectivity` |
| Policy Status | Opens Policy page | `StatusStrip_PolicyStatus_OpensPolicy` |
| Assistant Status | Opens Assistant page | `StatusStrip_AssistantStatus_OpensAssistant` |
| Memory Status | Opens Settings page | `StatusStrip_MemoryStatus_OpensSettings` |

## Settings Page Controls

| Control | Expected Result | Test Name |
|---------|-----------------|-----------|
| Back Button | Returns to previous page | `Settings_BackButton_NavigatesBack` |
| Theme Selector | Changes app theme | `Settings_ThemeSelector_ChangesTheme` |
| Model Manager Link | Opens Model Manager | `Settings_ModelManagerLink_OpensModelManager` |
| Extensions Link | Opens Extensions page | `Settings_ExtensionsLink_OpensExtensions` |
| Policy Link | Opens Policy page | `Settings_PolicyLink_OpensPolicy` |
| Export Settings | Exports settings file | `Settings_ExportButton_ExportsFile` |
| Import Settings | Opens import dialog | `Settings_ImportButton_OpensDialog` |
| Reset Settings | Shows confirmation | `Settings_ResetButton_ShowsConfirmation` |
| Clear Cache | Clears app cache | `Settings_ClearCacheButton_ClearsCache` |
| Export Logs | Exports log bundle | `Settings_ExportLogsButton_ExportsLogs` |
| Support Bundle | Creates support bundle | `Settings_SupportBundleButton_CreatesBundle` |
| About Link | Shows version info | `Settings_AboutLink_ShowsVersion` |

## Model Manager Controls

| Control | Expected Result | Test Name |
|---------|-----------------|-----------|
| Back Button | Returns to previous page | `ModelManager_BackButton_NavigatesBack` |
| Refresh Button | Refreshes model list | `ModelManager_RefreshButton_RefreshesList` |
| Pull Model Button | Opens pull dialog | `ModelManager_PullButton_OpensDialog` |
| Default Model Selector | Sets default model | `ModelManager_DefaultSelector_SetsDefault` |
| Quick Pull: llama3.2 | Pulls llama3.2 model | `ModelManager_QuickPull_Llama32` |
| Quick Pull: mistral | Pulls mistral model | `ModelManager_QuickPull_Mistral` |
| Quick Pull: codegemma | Pulls codegemma model | `ModelManager_QuickPull_Codegemma` |
| Ollama Docs Link | Opens Ollama docs | `ModelManager_DocsLink_OpensUrl` |
| Ollama Library Link | Opens Ollama library | `ModelManager_LibraryLink_OpensUrl` |
| Model List Item | Selects model | `ModelManager_ModelItem_SelectsModel` |

## Help Page Controls

| Control | Expected Result | Test Name |
|---------|-----------------|-----------|
| Back Button | Returns to previous page | `Help_BackButton_NavigatesBack` |
| Search Box | Filters help topics | `Help_SearchBox_FiltersTopics` |
| Topic Link | Expands topic content | `Help_TopicLink_ExpandsContent` |
| Model Manager Link | Opens Model Manager | `Help_ModelManagerLink_OpensModelManager` |

## Command Palette Controls

| Control | Expected Result | Test Name |
|---------|-----------------|-----------|
| Search Input | Filters commands | `CommandPalette_Search_FiltersCommands` |
| New Session | Creates new session | `CommandPalette_NewSession_CreatesSession` |
| Open Settings | Opens Settings | `CommandPalette_OpenSettings_OpensSettings` |
| Open Model Manager | Opens Model Manager | `CommandPalette_OpenModelManager_OpensModelManager` |
| Toggle Offline | Toggles offline mode | `CommandPalette_ToggleOffline_TogglesMode` |
| Open Extensions | Opens Extensions | `CommandPalette_OpenExtensions_OpensExtensions` |
| Open Assistant | Opens Assistant | `CommandPalette_OpenAssistant_OpensAssistant` |
| View Policy | Opens Policy | `CommandPalette_ViewPolicy_OpensPolicy` |
| Open Connectivity | Opens Connectivity | `CommandPalette_OpenConnectivity_OpensConnectivity` |
| Open Help | Opens Help | `CommandPalette_OpenHelp_OpensHelp` |

## Session Sidebar Controls

| Control | Expected Result | Test Name |
|---------|-----------------|-----------|
| New Session Button | Creates new session | `Sidebar_NewSession_CreatesSession` |
| Session Item | Selects session | `Sidebar_SessionItem_SelectsSession` |
| Search Sessions | Filters session list | `Sidebar_Search_FiltersSessions` |
| Delete Session | Shows confirmation | `Sidebar_DeleteSession_ShowsConfirmation` |

## Input Composer Controls

| Control | Expected Result | Test Name |
|---------|-----------------|-----------|
| Text Input | Accepts text | `InputComposer_TextInput_AcceptsText` |
| Send Button | Sends message | `InputComposer_SendButton_SendsMessage` |
| Stop Button | Stops generation | `InputComposer_StopButton_StopsGeneration` |
| Attachment Button | Opens file picker | `InputComposer_AttachmentButton_OpensPicker` |

---

## Keyboard Shortcuts

| Shortcut | Expected Result | Test Name |
|----------|-----------------|-----------|
| Ctrl+K | Opens Command Palette | `Keyboard_CtrlK_OpensCommandPalette` |
| Ctrl+N | Creates new session | `Keyboard_CtrlN_CreatesSession` |
| Ctrl+Enter | Sends message | `Keyboard_CtrlEnter_SendsMessage` |
| Escape | Closes overlay/goes back | `Keyboard_Escape_ClosesOrGoesBack` |
| Alt+Left | Navigates back | `Keyboard_AltLeft_NavigatesBack` |

---

## Test Execution

### Running Tests

```powershell
# Run all UI tests
dotnet test tests/InControl.UITests

# Run specific category
dotnet test tests/InControl.UITests --filter "Category=AppBar"

# Run with verbose output
dotnet test tests/InControl.UITests -v detailed
```

### CI Integration

Tests run automatically on:
- Pull requests to `main`
- Release builds
- Nightly validation

### Adding New Controls

When adding new buttons/controls:
1. Add row to this matrix
2. Create test in appropriate test class
3. Verify test passes locally
4. Submit PR with both changes
