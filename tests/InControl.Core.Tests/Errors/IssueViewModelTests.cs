using FluentAssertions;
using InControl.ViewModels.Errors;
using Xunit;

namespace InControl.Core.Tests.Errors;

public class IssueViewModelTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var issue = new IssueViewModel("Title", "Detail", IssueSeverity.Warning);

        issue.Title.Should().Be("Title");
        issue.Detail.Should().Be("Detail");
        issue.Severity.Should().Be(IssueSeverity.Warning);
    }

    [Fact]
    public void Id_IsUnique()
    {
        var issue1 = new IssueViewModel("A", "A");
        var issue2 = new IssueViewModel("B", "B");

        issue1.Id.Should().NotBe(issue2.Id);
    }

    [Fact]
    public void SeverityIcon_VariesBySeverity()
    {
        var info = new IssueViewModel("", "", IssueSeverity.Info);
        var warning = new IssueViewModel("", "", IssueSeverity.Warning);
        var critical = new IssueViewModel("", "", IssueSeverity.Critical);

        info.SeverityIcon.Should().NotBe(warning.SeverityIcon);
        warning.SeverityIcon.Should().NotBe(critical.SeverityIcon);
    }

    [Fact]
    public void WithAction_AddsRecoveryAction()
    {
        var actionExecuted = false;
        var issue = new IssueViewModel("Title", "Detail")
            .WithAction("Test", () => actionExecuted = true);

        issue.RecoveryActions.Should().HaveCount(1);
        issue.HasRecoveryActions.Should().BeTrue();

        issue.RecoveryActions[0].Execute();
        actionExecuted.Should().BeTrue();
    }

    [Fact]
    public void WithAction_SupportsPrimaryFlag()
    {
        var issue = new IssueViewModel("Title", "Detail")
            .WithAction("Primary", () => { }, isPrimary: true)
            .WithAction("Secondary", () => { }, isPrimary: false);

        issue.RecoveryActions[0].IsPrimary.Should().BeTrue();
        issue.RecoveryActions[1].IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void Dismiss_SetsIsDismissed()
    {
        var issue = new IssueViewModel("Title", "Detail");

        issue.Dismiss();

        issue.IsDismissed.Should().BeTrue();
    }

    [Fact]
    public void OccurredAt_SetToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var issue = new IssueViewModel("Title", "Detail");
        var after = DateTimeOffset.UtcNow;

        issue.OccurredAt.Should().BeOnOrAfter(before);
        issue.OccurredAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void PropertyChanged_RaisedForTitle()
    {
        var issue = new IssueViewModel("Original", "Detail");
        var changed = new List<string>();
        issue.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        issue.Title = "New Title";

        changed.Should().Contain(nameof(issue.Title));
    }
}

public class IssueFactoryMethodTests
{
    [Fact]
    public void ConnectionUnavailable_CreatesCorrectIssue()
    {
        var issue = IssueViewModel.ConnectionUnavailable("localhost:11434");

        issue.Title.Should().Be("Connection unavailable");
        issue.Detail.Should().Contain("localhost:11434");
        issue.Severity.Should().Be(IssueSeverity.Critical);
        issue.RecoveryActions.Should().HaveCount(2);
    }

    [Fact]
    public void ModelNotFound_CreatesCorrectIssue()
    {
        var issue = IssueViewModel.ModelNotFound("llama3.2");

        issue.Title.Should().Be("Model not found");
        issue.Detail.Should().Contain("llama3.2");
        issue.Severity.Should().Be(IssueSeverity.Warning);
        issue.RecoveryActions.Should().HaveCount(2);
    }

    [Fact]
    public void ContextLimitExceeded_CreatesCorrectIssue()
    {
        var issue = IssueViewModel.ContextLimitExceeded(10000, 8192);

        issue.Title.Should().Be("Context limit exceeded");
        issue.Detail.Should().Contain("8,192");
        issue.Detail.Should().Contain("10,000");
        issue.Severity.Should().Be(IssueSeverity.Warning);
    }

    [Fact]
    public void OutOfMemory_CreatesCorrectIssue()
    {
        var issue = IssueViewModel.OutOfMemory(8L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024);

        issue.Title.Should().Be("Insufficient GPU memory");
        issue.Detail.Should().Contain("GB");
        issue.Severity.Should().Be(IssueSeverity.Critical);
    }

    [Fact]
    public void ExecutionInterrupted_CreatesCorrectIssue()
    {
        var issue = IssueViewModel.ExecutionInterrupted("Connection lost");

        issue.Title.Should().Be("Execution interrupted");
        issue.Detail.Should().Be("Connection lost");
        issue.Severity.Should().Be(IssueSeverity.Warning);
        issue.RecoveryActions.Should().HaveCount(1);
    }
}

public class RecoveryActionTests
{
    [Fact]
    public void Execute_InvokesAction()
    {
        var executed = false;
        var action = new RecoveryAction("Test", () => executed = true);

        action.Execute();

        executed.Should().BeTrue();
    }

    [Fact]
    public void Label_SetCorrectly()
    {
        var action = new RecoveryAction("My Label", () => { });

        action.Label.Should().Be("My Label");
    }

    [Fact]
    public void IsPrimary_SetCorrectly()
    {
        var primary = new RecoveryAction("Primary", () => { }, isPrimary: true);
        var secondary = new RecoveryAction("Secondary", () => { }, isPrimary: false);

        primary.IsPrimary.Should().BeTrue();
        secondary.IsPrimary.Should().BeFalse();
    }
}
