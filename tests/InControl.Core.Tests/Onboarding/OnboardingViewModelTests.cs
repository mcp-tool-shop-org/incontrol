using FluentAssertions;
using InControl.ViewModels.Onboarding;
using Xunit;

namespace InControl.Core.Tests.Onboarding;

public class OnboardingViewModelTests
{
    [Fact]
    public void CurrentStep_DefaultsToWelcome()
    {
        var vm = new OnboardingViewModel();

        vm.CurrentStep.Should().Be(OnboardingStep.Welcome);
        vm.ShowWelcome.Should().BeTrue();
    }

    [Fact]
    public void StepNumber_IsOneBased()
    {
        var vm = new OnboardingViewModel();

        vm.StepNumber.Should().Be(1);
        vm.TotalSteps.Should().Be(4);
    }

    [Fact]
    public void StepTitle_VariesByStep()
    {
        var vm = new OnboardingViewModel();

        vm.StepTitle.Should().Be("Welcome to InControl");

        vm.CurrentStep = OnboardingStep.BackendCheck;
        vm.StepTitle.Should().Be("Connect to Backend");

        vm.CurrentStep = OnboardingStep.ModelSelection;
        vm.StepTitle.Should().Be("Select a Model");

        vm.CurrentStep = OnboardingStep.Ready;
        vm.StepTitle.Should().Be("Ready to Go");
    }

    [Fact]
    public void ShowStepFlags_ReflectCurrentStep()
    {
        var vm = new OnboardingViewModel();

        vm.ShowWelcome.Should().BeTrue();
        vm.ShowBackendCheck.Should().BeFalse();

        vm.CurrentStep = OnboardingStep.BackendCheck;
        vm.ShowWelcome.Should().BeFalse();
        vm.ShowBackendCheck.Should().BeTrue();
    }

    [Fact]
    public void CanGoBack_FalseOnFirstStep()
    {
        var vm = new OnboardingViewModel();

        vm.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void CanGoBack_TrueOnLaterSteps()
    {
        var vm = new OnboardingViewModel();
        vm.CurrentStep = OnboardingStep.BackendCheck;

        vm.CanGoBack.Should().BeTrue();
    }

    [Fact]
    public void CanGoNext_TrueOnWelcome()
    {
        var vm = new OnboardingViewModel();

        vm.CanGoNext.Should().BeTrue();
    }

    [Fact]
    public void CanGoNext_RequiresBackendConnection()
    {
        var vm = new OnboardingViewModel();
        vm.CurrentStep = OnboardingStep.BackendCheck;

        vm.CanGoNext.Should().BeFalse();

        vm.IsBackendConnected = true;
        vm.CanGoNext.Should().BeTrue();
    }

    [Fact]
    public void CanGoNext_RequiresModelSelection()
    {
        var vm = new OnboardingViewModel();
        vm.CurrentStep = OnboardingStep.ModelSelection;

        vm.CanGoNext.Should().BeFalse();

        vm.SelectedModel = "llama3.2";
        vm.CanGoNext.Should().BeTrue();
    }

    [Fact]
    public void GoNext_AdvancesStep()
    {
        var vm = new OnboardingViewModel();

        vm.GoNext();

        vm.CurrentStep.Should().Be(OnboardingStep.BackendCheck);
    }

    [Fact]
    public void GoNext_DoesNotAdvance_WhenCannotProceed()
    {
        var vm = new OnboardingViewModel();
        vm.CurrentStep = OnboardingStep.BackendCheck;

        vm.GoNext();

        vm.CurrentStep.Should().Be(OnboardingStep.BackendCheck);
    }

    [Fact]
    public void GoNext_CompletesOnReadyStep()
    {
        var vm = new OnboardingViewModel();
        vm.CurrentStep = OnboardingStep.Ready;

        vm.GoNext();

        vm.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void GoBack_ReturnsToPreviousStep()
    {
        var vm = new OnboardingViewModel();
        vm.CurrentStep = OnboardingStep.BackendCheck;

        vm.GoBack();

        vm.CurrentStep.Should().Be(OnboardingStep.Welcome);
    }

    [Fact]
    public void GoBack_DoesNothing_OnFirstStep()
    {
        var vm = new OnboardingViewModel();

        vm.GoBack();

        vm.CurrentStep.Should().Be(OnboardingStep.Welcome);
    }

    [Fact]
    public void Skip_SetsIsComplete()
    {
        var vm = new OnboardingViewModel();

        vm.Skip();

        vm.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var vm = new OnboardingViewModel();
        vm.CurrentStep = OnboardingStep.Ready;
        vm.IsBackendConnected = true;
        vm.SelectedModel = "test";
        vm.Complete();

        vm.Reset();

        vm.CurrentStep.Should().Be(OnboardingStep.Welcome);
        vm.IsComplete.Should().BeFalse();
        vm.IsBackendConnected.Should().BeFalse();
        vm.SelectedModel.Should().BeEmpty();
    }

    [Fact]
    public void BackendStatusText_ReflectsConnectionState()
    {
        var vm = new OnboardingViewModel();

        vm.BackendStatusText.Should().Be("Searching for backend...");

        vm.DetectedBackend = "localhost:11434";
        vm.IsBackendConnected = true;
        vm.BackendStatusText.Should().Be("Connected to localhost:11434");
    }

    [Fact]
    public void HasDetectedBackend_ReflectsEndpoint()
    {
        var vm = new OnboardingViewModel();

        vm.HasDetectedBackend.Should().BeFalse();

        vm.DetectedBackend = "localhost:11434";
        vm.HasDetectedBackend.Should().BeTrue();
    }

    [Fact]
    public void HasSelectedModel_ReflectsSelection()
    {
        var vm = new OnboardingViewModel();

        vm.HasSelectedModel.Should().BeFalse();

        vm.SelectedModel = "llama3.2";
        vm.HasSelectedModel.Should().BeTrue();
    }

    [Fact]
    public void PropertyChanged_RaisedForCurrentStep()
    {
        var vm = new OnboardingViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        vm.CurrentStep = OnboardingStep.BackendCheck;

        changed.Should().Contain(nameof(vm.CurrentStep));
        changed.Should().Contain(nameof(vm.ShowWelcome));
        changed.Should().Contain(nameof(vm.ShowBackendCheck));
    }
}

public class ReentryViewModelTests
{
    [Fact]
    public void WelcomeGreeting_VariesByTimeOfDay()
    {
        var vm = new ReentryViewModel();

        // Just verify it returns something
        vm.WelcomeGreeting.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LastSessionTimeText_FormatsCorrectly()
    {
        var vm = new ReentryViewModel();

        vm.LastSessionTime = null;
        vm.LastSessionTimeText.Should().BeEmpty();

        vm.LastSessionTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        vm.LastSessionTimeText.Should().Be("5m ago");

        vm.LastSessionTime = DateTimeOffset.UtcNow.AddHours(-3);
        vm.LastSessionTimeText.Should().Be("3h ago");

        vm.LastSessionTime = DateTimeOffset.UtcNow.AddDays(-2);
        vm.LastSessionTimeText.Should().Be("2d ago");
    }

    [Fact]
    public void LastSessionTimeText_JustNow()
    {
        var vm = new ReentryViewModel();
        vm.LastSessionTime = DateTimeOffset.UtcNow.AddSeconds(-30);

        vm.LastSessionTimeText.Should().Be("Just now");
    }

    [Fact]
    public void TotalSessionsText_FormatsCorrectly()
    {
        var vm = new ReentryViewModel();

        vm.TotalSessions = 1;
        vm.TotalSessionsText.Should().Be("1 session");

        vm.TotalSessions = 5;
        vm.TotalSessionsText.Should().Be("5 sessions");
    }

    [Fact]
    public void HasLastSession_ReflectsTitle()
    {
        var vm = new ReentryViewModel();

        vm.HasLastSession.Should().BeFalse();

        vm.LastSessionTitle = "My Session";
        vm.HasLastSession.Should().BeTrue();
    }

    [Fact]
    public void HasLastUsedModel_ReflectsModel()
    {
        var vm = new ReentryViewModel();

        vm.HasLastUsedModel.Should().BeFalse();

        vm.LastUsedModel = "llama3.2";
        vm.HasLastUsedModel.Should().BeTrue();
    }

    [Fact]
    public void UpdateFromHistory_SetsProperties()
    {
        var vm = new ReentryViewModel();
        var time = DateTimeOffset.UtcNow.AddMinutes(-10);

        vm.UpdateFromHistory("Test Session", time, "llama3.2", 5);

        vm.LastSessionTitle.Should().Be("Test Session");
        vm.LastSessionTime.Should().Be(time);
        vm.LastUsedModel.Should().Be("llama3.2");
        vm.TotalSessions.Should().Be(5);
    }

    [Fact]
    public void UpdateFromHistory_SuggestsContinue_WhenRecentSession()
    {
        var vm = new ReentryViewModel();
        var recentTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        vm.UpdateFromHistory("Recent Session", recentTime, "llama3.2", 3);

        vm.HasRecentSession.Should().BeTrue();
        vm.SuggestedAction.Should().Be(ReentryAction.ContinueSession);
    }

    [Fact]
    public void UpdateFromHistory_SuggestsBrowse_WhenManySessions()
    {
        var vm = new ReentryViewModel();
        var oldTime = DateTimeOffset.UtcNow.AddDays(-7);

        vm.UpdateFromHistory("Old Session", oldTime, "llama3.2", 10);

        vm.HasRecentSession.Should().BeFalse();
        vm.SuggestedAction.Should().Be(ReentryAction.BrowseSessions);
    }

    [Fact]
    public void UpdateFromHistory_SuggestsNew_WhenFewSessions()
    {
        var vm = new ReentryViewModel();

        vm.UpdateFromHistory(null, null, null, 2);

        vm.SuggestedAction.Should().Be(ReentryAction.NewSession);
    }

    [Fact]
    public void SuggestedActionText_ReflectsAction()
    {
        var vm = new ReentryViewModel();
        vm.LastSessionTitle = "My Session";

        vm.SuggestedAction = ReentryAction.ContinueSession;
        vm.SuggestedActionText.Should().Contain("My Session");

        vm.SuggestedAction = ReentryAction.NewSession;
        vm.SuggestedActionText.Should().Be("Start a new session");

        vm.SuggestedAction = ReentryAction.BrowseSessions;
        vm.SuggestedActionText.Should().Be("Browse your sessions");
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var vm = new ReentryViewModel();
        vm.UpdateFromHistory("Test", DateTimeOffset.UtcNow, "model", 5);

        vm.Reset();

        vm.LastSessionTitle.Should().BeEmpty();
        vm.LastSessionTime.Should().BeNull();
        vm.LastUsedModel.Should().BeEmpty();
        vm.TotalSessions.Should().Be(0);
        vm.SuggestedAction.Should().Be(ReentryAction.NewSession);
    }

    [Fact]
    public void PropertyChanged_RaisedForLastSessionTitle()
    {
        var vm = new ReentryViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        vm.LastSessionTitle = "New Title";

        changed.Should().Contain(nameof(vm.LastSessionTitle));
        changed.Should().Contain(nameof(vm.HasLastSession));
    }
}
