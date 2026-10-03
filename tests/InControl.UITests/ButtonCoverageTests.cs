using System.Text.RegularExpressions;
using Xunit;

namespace InControl.UITests;

/// <summary>
/// Structural checks on the app's XAML and code-behind, read as text.
///
/// These do not launch the UI. Each fact fails if a named control is removed from the XAML,
/// if its Click handler is no longer wired, or if the event the shell listens to is no longer
/// raised. They do not prove the control renders or that a user can click it; that needs a UI
/// automation harness (WinAppDriver or similar), which this project does not have.
/// </summary>
public class ButtonCoverageTests
{
    private static readonly string AppRoot = FindAppRoot();

    private static string FindAppRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "InControl.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, "Could not find InControl.sln above " + AppContext.BaseDirectory);
        var root = Path.Combine(dir!.FullName, "src", "InControl.App");
        Assert.True(Directory.Exists(root), "Missing app source folder: " + root);
        return root;
    }

    private static string Source(string relativePath)
    {
        var path = Path.Combine(AppRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), "Missing source file: " + path);
        return File.ReadAllText(path);
    }

    private static void AssertNamedInXaml(string xaml, string name, string file)
    {
        Assert.True(
            Regex.IsMatch(xaml, "x:Name=\"" + Regex.Escape(name) + "\""),
            $"{file} has no element named {name}");
    }

    private static void AssertEventDeclared(string code, string eventName, string file)
    {
        Assert.True(
            Regex.IsMatch(code, @"public\s+event\s+EventHandler(<[^>]+>)?\??\s+" + Regex.Escape(eventName) + @"\s*;"),
            $"{file} does not declare public event {eventName}");
    }

    private static void AssertEventRaised(string code, string eventName, string file)
    {
        Assert.True(
            Regex.IsMatch(code, Regex.Escape(eventName) + @"\?\.Invoke\("),
            $"{file} never raises {eventName}");
    }

    private static void AssertClickWired(string code, string control, string file)
    {
        Assert.True(
            Regex.IsMatch(code, @"\b" + Regex.Escape(control) + @"\.Click\s*\+="),
            $"{file} does not subscribe to {control}.Click");
    }

    private static void AssertButtonRaisesEvent(string folder, string type, string button, string eventName)
    {
        var xaml = Source($"{folder}/{type}.xaml");
        var code = Source($"{folder}/{type}.xaml.cs");
        AssertNamedInXaml(xaml, button, type + ".xaml");
        AssertClickWired(code, button, type + ".xaml.cs");
        AssertEventDeclared(code, eventName, type + ".xaml.cs");
        AssertEventRaised(code, eventName, type + ".xaml.cs");
    }

    #region AppBar Tests

    [Theory]
    [Trait("Category", "AppBar")]
    [InlineData("SettingsButton", "SettingsRequested")]
    [InlineData("ModelManagerButton", "ModelManagerRequested")]
    [InlineData("AssistantButton", "AssistantRequested")]
    [InlineData("ExtensionsButton", "ExtensionsRequested")]
    [InlineData("PolicyButton", "PolicyRequested")]
    [InlineData("ConnectivityButton", "ConnectivityRequested")]
    [InlineData("HelpButton", "HelpRequested")]
    [InlineData("SearchButton", "CommandPaletteRequested")]
    [InlineData("HomeButton", "HomeRequested")]
    [InlineData("CancelButton", "CancelRequested")]
    public void AppBar_Button_IsWiredToItsEvent(string button, string eventName)
    {
        AssertButtonRaisesEvent("Controls", "AppBar", button, eventName);
    }

    [Theory]
    [Trait("Category", "AppBar")]
    [InlineData("SettingsRequested")]
    [InlineData("ModelManagerRequested")]
    [InlineData("AssistantRequested")]
    [InlineData("ExtensionsRequested")]
    [InlineData("PolicyRequested")]
    [InlineData("ConnectivityRequested")]
    [InlineData("HelpRequested")]
    [InlineData("CommandPaletteRequested")]
    public void AppBar_Event_IsHandledByMainWindow(string eventName)
    {
        var main = Source("MainWindow.xaml.cs");
        Assert.True(
            Regex.IsMatch(main, @"AppBar\." + Regex.Escape(eventName) + @"\s*\+="),
            $"MainWindow.xaml.cs does not subscribe to AppBar.{eventName}");
    }

    #endregion

    #region StatusStrip Tests

    [Theory]
    [Trait("Category", "StatusStrip")]
    [InlineData("ModelStatusButton", "ModelClicked")]
    [InlineData("DeviceStatusButton", "DeviceClicked")]
    [InlineData("ConnectivityButton", "ConnectivityClicked")]
    [InlineData("PolicyButton", "PolicyClicked")]
    [InlineData("AssistantButton", "AssistantClicked")]
    [InlineData("MemoryButton", "MemoryClicked")]
    public void StatusStrip_Button_IsWiredToItsEvent(string button, string eventName)
    {
        AssertButtonRaisesEvent("Controls", "StatusStrip", button, eventName);
    }

    #endregion

    #region ModelManager Tests

    [Theory]
    [Trait("Category", "ModelManager")]
    [InlineData("BackButton")]
    [InlineData("RefreshOllamaButton")]
    [InlineData("PullModelButton")]
    [InlineData("PullLlama32Button")]
    [InlineData("PullMistralButton")]
    [InlineData("PullCodegemmaButton")]
    [InlineData("OpenOllamaDocsButton")]
    [InlineData("OpenOllamaLibraryButton")]
    public void ModelManager_Button_ExistsAndIsWired(string button)
    {
        AssertNamedInXaml(Source("Pages/ModelManagerPage.xaml"), button, "ModelManagerPage.xaml");
        AssertClickWired(Source("Pages/ModelManagerPage.xaml.cs"), button, "ModelManagerPage.xaml.cs");
    }

    [Fact]
    [Trait("Category", "ModelManager")]
    public void ModelManager_DefaultModelSelector_ExistsAndReportsChanges()
    {
        AssertNamedInXaml(Source("Pages/ModelManagerPage.xaml"), "DefaultModelSelector", "ModelManagerPage.xaml");
        Assert.Matches(@"DefaultModelSelector\.SelectionChanged\s*\+=", Source("Pages/ModelManagerPage.xaml.cs"));
    }

    [Fact]
    [Trait("Category", "ModelManager")]
    public void ModelManager_QuickPullButtons_PullTheModelTheyAreNamedFor()
    {
        var code = Source("Pages/ModelManagerPage.xaml.cs");
        Assert.Matches(@"PullLlama32Button\.Click\s*\+=[^;]*PullModelAsync\(""llama3\.2""\)", code);
        Assert.Matches(@"PullMistralButton\.Click\s*\+=[^;]*PullModelAsync\(""mistral""\)", code);
        Assert.Matches(@"PullCodegemmaButton\.Click\s*\+=[^;]*PullModelAsync\(""codegemma""\)", code);
    }

    #endregion

    #region CommandPalette Tests

    [Fact]
    [Trait("Category", "CommandPalette")]
    public void CommandPalette_SearchInputAndList_Exist()
    {
        var xaml = Source("Controls/CommandPalette.xaml");
        AssertNamedInXaml(xaml, "SearchInput", "CommandPalette.xaml");
        AssertNamedInXaml(xaml, "CommandsList", "CommandPalette.xaml");
        var code = Source("Controls/CommandPalette.xaml.cs");
        Assert.Matches(@"SearchInput\.TextChanged\s*\+=", code);
        Assert.Matches(@"CommandsList\.ItemClick\s*\+=", code);
    }

    [Theory]
    [Trait("Category", "CommandPalette")]
    [InlineData("CommandExecuted")]
    [InlineData("CloseRequested")]
    public void CommandPalette_Event_IsDeclaredAndRaised(string eventName)
    {
        var code = Source("Controls/CommandPalette.xaml.cs");
        AssertEventDeclared(code, eventName, "CommandPalette.xaml.cs");
        AssertEventRaised(code, eventName, "CommandPalette.xaml.cs");
    }

    #endregion

    #region SessionSidebar Tests

    [Fact]
    [Trait("Category", "SessionSidebar")]
    public void SessionSidebar_NewSessionButton_RaisesNewSessionRequested()
    {
        AssertButtonRaisesEvent("Controls", "SessionSidebar", "NewSessionButton", "NewSessionRequested");
    }

    [Fact]
    [Trait("Category", "SessionSidebar")]
    public void SessionSidebar_SessionList_Exists()
    {
        AssertNamedInXaml(Source("Controls/SessionSidebar.xaml"), "SessionList", "SessionSidebar.xaml");
        AssertEventDeclared(Source("Controls/SessionSidebar.xaml.cs"), "SessionSelected", "SessionSidebar.xaml.cs");
    }

    #endregion

    #region InputComposer Tests

    [Fact]
    [Trait("Category", "InputComposer")]
    public void InputComposer_TextInputAndActionButton_Exist()
    {
        var xaml = Source("Controls/InputComposer.xaml");
        AssertNamedInXaml(xaml, "IntentInput", "InputComposer.xaml");
        AssertNamedInXaml(xaml, "ActionButton", "InputComposer.xaml");
        AssertClickWired(Source("Controls/InputComposer.xaml.cs"), "ActionButton", "InputComposer.xaml.cs");
    }

    [Fact]
    [Trait("Category", "InputComposer")]
    public void InputComposer_RunRequested_IsDeclaredAndRaised()
    {
        var code = Source("Controls/InputComposer.xaml.cs");
        AssertEventDeclared(code, "RunRequested", "InputComposer.xaml.cs");
        AssertEventRaised(code, "RunRequested", "InputComposer.xaml.cs");
    }

    #endregion

    #region Keyboard Shortcut Tests

    [Fact]
    [Trait("Category", "Keyboard")]
    public void Keyboard_CtrlK_OpensCommandPalette()
    {
        var main = Source("MainWindow.xaml.cs");
        Assert.Matches(
            @"VirtualKey\.K\s*&&[^{]*VirtualKey\.Control[^{]*\{\s*ShowCommandPalette\(\)",
            main);
    }

    [Fact]
    [Trait("Category", "Keyboard")]
    public void Keyboard_Escape_IsRegisteredAsAnAccelerator()
    {
        var main = Source("MainWindow.xaml.cs");
        Assert.Matches(@"new KeyboardAccelerator\s*\{\s*Key\s*=\s*Windows\.System\.VirtualKey\.Escape\s*\}", main);
        Assert.Matches(@"KeyboardAccelerators\.Add\(escAccelerator\)", main);
    }

    [Fact]
    [Trait("Category", "Keyboard")]
    public void Keyboard_Escape_CancelsRunThenClosesPaletteThenGoesBack()
    {
        var main = Source("MainWindow.xaml.cs");
        var start = main.IndexOf("VirtualKey.Escape)", StringComparison.Ordinal);
        Assert.True(start >= 0, "MainWindow.xaml.cs has no Escape handler in OnGlobalKeyDown");
        var block = main.Substring(start, Math.Min(900, main.Length - start));

        var cancel = block.IndexOf("OnCancelRequested", StringComparison.Ordinal);
        var palette = block.IndexOf("HideCommandPalette", StringComparison.Ordinal);
        var back = block.IndexOf("_navigation.GoBack", StringComparison.Ordinal);
        Assert.True(cancel >= 0 && palette > cancel && back > palette,
            "Escape must cancel a running generation first, then close the palette, then go back");
    }

    [Fact]
    [Trait("Category", "Keyboard")]
    public void Keyboard_AltLeft_GoesBackOnlyWhenThereIsSomewhereToGo()
    {
        var main = Source("MainWindow.xaml.cs");
        Assert.Matches(
            @"VirtualKey\.Left\s*&&[^{]*VirtualKey\.Menu[^{]*\{\s*if \(_navigation\.CanGoBack\)\s*\{\s*_navigation\.GoBack\(\)",
            main);
    }

    #endregion

    #region Navigation Tests

    public static TheoryData<string, string> Pages => new()
    {
        { "SettingsPage", "settings" },
        { "ModelManagerPage", "modelManager" },
        { "AssistantPage", "assistant" },
        { "ExtensionsPage", "extensions" },
        { "PolicyPage", "policy" },
        { "ConnectivityPage", "connectivity" },
        { "HelpPage", "help" }
    };

    [Theory]
    [Trait("Category", "Navigation")]
    [MemberData(nameof(Pages))]
    public void Navigation_PageHasBackButtonThatRaisesBackRequested(string page, string _)
    {
        var xaml = Source($"Pages/{page}.xaml");
        var code = Source($"Pages/{page}.xaml.cs");

        AssertNamedInXaml(xaml, "BackButton", page + ".xaml");
        AssertEventDeclared(code, "BackRequested", page + ".xaml.cs");
        Assert.Matches(@"BackButton\.Click\s*\+=\s*\([^)]*\)\s*=>\s*BackRequested\?\.Invoke\(", code);
    }

    [Theory]
    [Trait("Category", "Navigation")]
    [MemberData(nameof(Pages))]
    public void Navigation_MainWindowSendsBackRequestedToTheBackstack(string page, string variable)
    {
        var main = Source("MainWindow.xaml.cs");

        Assert.Matches(@"page is " + page + " " + variable + @"\b", main);
        // Each page's BackRequested handler must call GoBack on the shared navigation service.
        var pattern = variable + @"\.BackRequested\s*\+=\s*\(s, e\)\s*=>\s*(\{[^}]*_navigation\.GoBack\(\)|_navigation\.GoBack\(\))";
        Assert.Matches(pattern, main);
    }

    [Fact]
    [Trait("Category", "Navigation")]
    public void Navigation_Service_KeepsABackstackAndPopsIt()
    {
        var nav = Source("Services/NavigationService.cs");

        Assert.Matches(@"Stack<Type>\s+_navigationStack", nav);
        Assert.Matches(@"CanGoBack\s*=>\s*_navigationStack\.Count\s*>\s*0", nav);
        Assert.Matches(@"public bool GoBack\(\)", nav);
        Assert.Matches(@"_navigationStack\.Pop\(\)", nav);
    }

    #endregion
}
