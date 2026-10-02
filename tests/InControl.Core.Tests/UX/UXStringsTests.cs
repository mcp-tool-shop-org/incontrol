using FluentAssertions;
using InControl.Core.UX;
using Xunit;

namespace InControl.Core.Tests.UX;

public class UXStringsTests
{
    [Fact]
    public void App_HasCorrectName()
    {
        UXStrings.App.Name.Should().Be("InControl");
    }

    [Fact]
    public void Session_New_IsImperative()
    {
        UXStrings.Session.New.Should().Be("New session");
        UXStrings.Session.New.Should().NotContain("Create");
    }

    [Fact]
    public void Actions_AreImperative()
    {
        UXStrings.Actions.Copy.Should().Be("Copy");
        UXStrings.Actions.Export.Should().Be("Export");
        UXStrings.Actions.Search.Should().Be("Search");
    }

    [Fact]
    public void Execution_RunComplete_FormatsCorrectly()
    {
        var result = UXStrings.Execution.RunComplete(2.345, 847);

        result.Should().Contain("2.3s");
        result.Should().Contain("847 tokens");
    }

    [Fact]
    public void Execution_Elapsed_FormatsSeconds()
    {
        var elapsed = TimeSpan.FromSeconds(5.3);

        var result = UXStrings.Execution.Elapsed(elapsed);

        result.Should().Be("5.3s");
    }

    [Fact]
    public void Execution_Elapsed_FormatsMinutes()
    {
        var elapsed = TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(15));

        var result = UXStrings.Execution.Elapsed(elapsed);

        result.Should().Be("2:15");
    }

    [Fact]
    public void Model_NotFound_IncludesModelName()
    {
        var result = UXStrings.Model.NotFound("llama3.2");

        result.Should().Contain("llama3.2");
        result.Should().Contain("not available");
    }

    [Fact]
    public void Model_PullHint_FormatsCommand()
    {
        var result = UXStrings.Model.PullHint("llama3.2");

        result.Should().Be("Pull model with: ollama pull llama3.2");
    }

    [Fact]
    public void Connection_Unavailable_IncludesEndpoint()
    {
        var result = UXStrings.Connection.Unavailable("localhost:11434");

        result.Should().Contain("localhost:11434");
        result.Should().Contain("not responding");
    }

    [Fact]
    public void Context_LimitExceeded_FormatsTokenCount()
    {
        var result = UXStrings.Context.LimitExceeded(8192);

        result.Should().Contain("8,192 tokens");
        result.Should().Contain("context window");
    }

    [Fact]
    public void Welcome_LastWorking_IncludesTitle()
    {
        var result = UXStrings.Welcome.LastWorking("My Important Session");

        result.Should().Contain("My Important Session");
        result.Should().Contain("last working on");
    }

    [Fact]
    public void Time_Relative_JustNow()
    {
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-30);

        var result = UXStrings.Time.Relative(timestamp);

        result.Should().Be("Just now");
    }

    [Fact]
    public void Time_Relative_MinutesAgo()
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);

        var result = UXStrings.Time.Relative(timestamp);

        result.Should().Be("5 minutes ago");
    }

    [Fact]
    public void Time_Relative_HoursAgo()
    {
        var timestamp = DateTimeOffset.UtcNow.AddHours(-3);

        var result = UXStrings.Time.Relative(timestamp);

        result.Should().Be("3 hours ago");
    }

    [Fact]
    public void Time_Relative_SingularMinute()
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-1);

        var result = UXStrings.Time.Relative(timestamp);

        result.Should().Be("1 minute ago");
    }

    [Fact]
    public void Time_Relative_SingularHour()
    {
        var timestamp = DateTimeOffset.UtcNow.AddHours(-1);

        var result = UXStrings.Time.Relative(timestamp);

        result.Should().Be("1 hour ago");
    }

    [Fact]
    public void NoBlameLanguage_NoApologies()
    {
        // Verify none of our strings contain apologetic language
        var allStrings = new[]
        {
            UXStrings.Session.EmptyState,
            UXStrings.Session.NoSessions,
            UXStrings.Model.NoModels,
            UXStrings.Context.NoItems,
            UXStrings.Connection.Disconnected
        };

        foreach (var s in allStrings)
        {
            s.Should().NotContainEquivalentOf("sorry");
            s.Should().NotContainEquivalentOf("oops");
            s.Should().NotContainEquivalentOf("error");
            s.Should().NotContain("!");
        }
    }

    [Fact]
    public void Terminology_UsesSession_NotChat()
    {
        UXStrings.Session.New.Should().Contain("session");
        UXStrings.Session.New.Should().NotContain("chat");

        UXStrings.Session.EmptyState.Should().Contain("session");
        UXStrings.Session.EmptyState.Should().NotContain("chat");
    }

    [Fact]
    public void Terminology_UsesCancel_NotStop()
    {
        UXStrings.Execution.Cancel.Should().Be("Cancel");
    }
}
