using FluentAssertions;
using InControl.Core.Assistant;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class AssistantOnboardingTests : IDisposable
{
    private readonly string _tempPath;
    private readonly AssistantOnboarding _onboarding;

    public AssistantOnboardingTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"onboarding-test-{Guid.NewGuid()}.json");
        _onboarding = new AssistantOnboarding(_tempPath);
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }
    }

    [Fact]
    public void NewOnboarding_StartsAtWelcome()
    {
        _onboarding.CurrentStep.Should().Be(OnboardingStep.Welcome);
    }

    [Fact]
    public void NewOnboarding_IsNotComplete()
    {
        _onboarding.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void NewOnboarding_IsNotLocked()
    {
        _onboarding.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void StartOnboarding_SetsStartedAt()
    {
        var before = DateTimeOffset.UtcNow;
        _onboarding.StartOnboarding();
        var after = DateTimeOffset.UtcNow;

        _onboarding.State.StartedAt.Should().NotBeNull();
        _onboarding.State.StartedAt!.Value.Should().BeOnOrAfter(before);
        _onboarding.State.StartedAt!.Value.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void AdvanceStep_MovesToNextStep()
    {
        _onboarding.AdvanceStep();

        _onboarding.CurrentStep.Should().Be(OnboardingStep.Capabilities);
    }

    [Fact]
    public void AdvanceStep_ThroughAllSteps()
    {
        _onboarding.CurrentStep.Should().Be(OnboardingStep.Welcome);
        _onboarding.AdvanceStep();
        _onboarding.CurrentStep.Should().Be(OnboardingStep.Capabilities);
        _onboarding.AdvanceStep();
        _onboarding.CurrentStep.Should().Be(OnboardingStep.Limitations);
        _onboarding.AdvanceStep();
        _onboarding.CurrentStep.Should().Be(OnboardingStep.PrivacySettings);
        _onboarding.AdvanceStep();
        _onboarding.CurrentStep.Should().Be(OnboardingStep.SafetyAcknowledgment);
        _onboarding.AdvanceStep();
        _onboarding.CurrentStep.Should().Be(OnboardingStep.PersonalitySetup);
        _onboarding.AdvanceStep();
        _onboarding.CurrentStep.Should().Be(OnboardingStep.Complete);
    }

    [Fact]
    public void AdvanceStep_SetsCompletedAt_WhenComplete()
    {
        // Advance through all steps
        while (_onboarding.CurrentStep != OnboardingStep.Complete)
        {
            _onboarding.AdvanceStep();
        }

        _onboarding.IsComplete.Should().BeTrue();
        _onboarding.State.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void AdvanceStep_ReturnsFalse_WhenAlreadyComplete()
    {
        while (_onboarding.CurrentStep != OnboardingStep.Complete)
        {
            _onboarding.AdvanceStep();
        }

        var result = _onboarding.AdvanceStep();

        result.Should().BeFalse();
    }

    [Fact]
    public void AdvanceStep_RaisesEvent()
    {
        OnboardingStateChangedEventArgs? capturedArgs = null;
        _onboarding.StateChanged += (_, args) => capturedArgs = args;

        _onboarding.AdvanceStep();

        capturedArgs.Should().NotBeNull();
        capturedArgs!.State.CurrentStep.Should().Be(OnboardingStep.Capabilities);
    }

    [Fact]
    public void AcknowledgeCapability_RecordsCapability()
    {
        _onboarding.AcknowledgeCapability(CapabilityIds.Memory);

        _onboarding.HasAcknowledgedCapability(CapabilityIds.Memory).Should().BeTrue();
    }

    [Fact]
    public void AcknowledgeCapability_DoesNotDuplicate()
    {
        _onboarding.AcknowledgeCapability(CapabilityIds.Memory);
        _onboarding.AcknowledgeCapability(CapabilityIds.Memory);

        _onboarding.State.AcknowledgedCapabilities.Count(c => c == CapabilityIds.Memory)
            .Should().Be(1);
    }

    [Fact]
    public void AcceptSafetyAcknowledgment_RecordsAcknowledgment()
    {
        _onboarding.AcceptSafetyAcknowledgment(SafetyAcknowledgmentIds.Fallibility);

        _onboarding.HasAcceptedSafetyAcknowledgment(SafetyAcknowledgmentIds.Fallibility)
            .Should().BeTrue();
    }

    [Fact]
    public void SetPersonalityProfile_SavesProfile()
    {
        var profile = AssistantProfile.Detailed;

        _onboarding.SetPersonalityProfile(profile);

        _onboarding.State.SelectedProfile.Should().Be(profile);
    }

    [Fact]
    public void EnableSafetyLock_LocksAssistant()
    {
        _onboarding.EnableSafetyLock("Testing");

        _onboarding.IsLocked.Should().BeTrue();
        _onboarding.State.SafetyLock.Reason.Should().Be("Testing");
    }

    [Fact]
    public void EnableSafetyLock_SetsLockedAt()
    {
        var before = DateTimeOffset.UtcNow;
        _onboarding.EnableSafetyLock("Testing");
        var after = DateTimeOffset.UtcNow;

        _onboarding.State.SafetyLock.LockedAt.Should().NotBeNull();
        _onboarding.State.SafetyLock.LockedAt!.Value.Should().BeOnOrAfter(before);
        _onboarding.State.SafetyLock.LockedAt!.Value.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void EnableSafetyLock_RaisesEvent()
    {
        OnboardingStateChangedEventArgs? capturedArgs = null;
        _onboarding.StateChanged += (_, args) => capturedArgs = args;

        _onboarding.EnableSafetyLock("Testing");

        capturedArgs.Should().NotBeNull();
        capturedArgs!.State.SafetyLock.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void DisableSafetyLock_RequiresCorrectCode()
    {
        _onboarding.EnableSafetyLock("Testing");

        var result = _onboarding.DisableSafetyLock("wrong-code");

        result.Should().BeFalse();
        _onboarding.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void DisableSafetyLock_UnlocksWithCorrectCode()
    {
        _onboarding.EnableSafetyLock("Testing");

        var result = _onboarding.DisableSafetyLock("UNLOCK-ASSISTANT");

        result.Should().BeTrue();
        _onboarding.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void DisableSafetyLock_IncrementsUnlockCount()
    {
        _onboarding.EnableSafetyLock("Test 1");
        _onboarding.DisableSafetyLock("UNLOCK-ASSISTANT");
        _onboarding.EnableSafetyLock("Test 2");
        _onboarding.DisableSafetyLock("UNLOCK-ASSISTANT");

        _onboarding.State.SafetyLock.UnlockCount.Should().Be(2);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        _onboarding.AdvanceStep();
        _onboarding.AcknowledgeCapability(CapabilityIds.Memory);
        _onboarding.EnableSafetyLock("Testing");

        _onboarding.Reset();

        _onboarding.CurrentStep.Should().Be(OnboardingStep.Welcome);
        _onboarding.State.AcknowledgedCapabilities.Should().BeEmpty();
        _onboarding.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void GetCurrentStepContent_ReturnsContent()
    {
        var content = _onboarding.GetCurrentStepContent();

        content.Should().NotBeNull();
        content.Title.Should().NotBeNullOrEmpty();
        content.Description.Should().NotBeNullOrEmpty();
        content.Items.Should().NotBeEmpty();
        content.ActionLabel.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void State_PersistsToFile()
    {
        _onboarding.AdvanceStep();
        _onboarding.AcknowledgeCapability(CapabilityIds.Tools);

        // Create new instance to load from file
        var loaded = new AssistantOnboarding(_tempPath);

        loaded.CurrentStep.Should().Be(OnboardingStep.Capabilities);
        loaded.HasAcknowledgedCapability(CapabilityIds.Tools).Should().BeTrue();
    }
}

public class OnboardingStepContentTests
{
    [Theory]
    [InlineData(OnboardingStep.Welcome, "Welcome")]
    [InlineData(OnboardingStep.Capabilities, "What I Can Do")]
    [InlineData(OnboardingStep.Limitations, "What I Cannot Do")]
    [InlineData(OnboardingStep.PrivacySettings, "Privacy")]
    [InlineData(OnboardingStep.SafetyAcknowledgment, "Safety")]
    [InlineData(OnboardingStep.PersonalitySetup, "Personalize")]
    [InlineData(OnboardingStep.Complete, "Complete")]
    public void GetStepContent_HasCorrectTitle(OnboardingStep step, string expectedTitlePart)
    {
        var content = AssistantOnboarding.GetStepContent(step);

        content.Title.Should().Contain(expectedTitlePart);
    }

    [Fact]
    public void AllSteps_HaveItems()
    {
        foreach (OnboardingStep step in Enum.GetValues<OnboardingStep>())
        {
            var content = AssistantOnboarding.GetStepContent(step);
            content.Items.Should().NotBeNull();
        }
    }
}

public class SafetyGuardTests : IDisposable
{
    private readonly string _tempPath;
    private readonly AssistantOnboarding _onboarding;
    private readonly SafetyGuard _guard;

    public SafetyGuardTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"guard-test-{Guid.NewGuid()}.json");
        _onboarding = new AssistantOnboarding(_tempPath);
        _guard = new SafetyGuard(_onboarding);
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }
    }

    [Fact]
    public void CheckOperation_BlocksIfNotOnboarded()
    {
        var result = _guard.CheckOperation("test");

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("onboarding");
    }

    [Fact]
    public void CheckOperation_BlocksIfLocked()
    {
        CompleteOnboarding();
        _onboarding.EnableSafetyLock("Testing");

        var result = _guard.CheckOperation("test");

        result.Allowed.Should().BeFalse();
        result.RequiresUnlock.Should().BeTrue();
        result.Reason.Should().Contain("Testing");
    }

    [Fact]
    public void CheckOperation_AllowsAfterOnboarding()
    {
        CompleteOnboarding();

        var result = _guard.CheckOperation("test");

        result.Allowed.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteIfAllowedAsync_ExecutesWhenAllowed()
    {
        CompleteOnboarding();

        var result = await _guard.ExecuteIfAllowedAsync(
            "test",
            async () =>
            {
                await Task.Delay(1);
                return 42;
            });

        result.Success.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteIfAllowedAsync_ReturnsBlockedValue_WhenNotAllowed()
    {
        // Don't complete onboarding

        var result = await _guard.ExecuteIfAllowedAsync(
            "test",
            async () =>
            {
                await Task.Delay(1);
                return 42;
            },
            blockedValue: -1);

        result.Success.Should().BeFalse();
        result.Value.Should().Be(-1);
        result.BlockedReason.Should().NotBeNull();
    }

    private void CompleteOnboarding()
    {
        while (_onboarding.CurrentStep != OnboardingStep.Complete)
        {
            _onboarding.AdvanceStep();
        }
    }
}

public class SafetyLockStateTests
{
    [Fact]
    public void CreateUnlocked_IsNotLocked()
    {
        var state = SafetyLockState.CreateUnlocked();

        state.IsLocked.Should().BeFalse();
        state.LockedAt.Should().BeNull();
        state.Reason.Should().BeNull();
    }

    [Fact]
    public void CreateLocked_IsLocked()
    {
        var state = SafetyLockState.CreateLocked("Test reason");

        state.IsLocked.Should().BeTrue();
        state.LockedAt.Should().NotBeNull();
        state.Reason.Should().Be("Test reason");
    }
}

public class CapabilityIdsTests
{
    [Fact]
    public void AllIds_AreUnique()
    {
        var ids = new[]
        {
            CapabilityIds.Memory,
            CapabilityIds.Tools,
            CapabilityIds.Learning,
            CapabilityIds.Explainability
        };

        ids.Distinct().Should().HaveCount(ids.Length);
    }

    [Fact]
    public void AllIds_HaveCapabilityPrefix()
    {
        CapabilityIds.Memory.Should().StartWith("capability.");
        CapabilityIds.Tools.Should().StartWith("capability.");
        CapabilityIds.Learning.Should().StartWith("capability.");
        CapabilityIds.Explainability.Should().StartWith("capability.");
    }
}

public class SafetyAcknowledgmentIdsTests
{
    [Fact]
    public void AllIds_AreUnique()
    {
        var ids = new[]
        {
            SafetyAcknowledgmentIds.Fallibility,
            SafetyAcknowledgmentIds.NotProfessionalAdvice,
            SafetyAcknowledgmentIds.SafetyLockAvailable,
            SafetyAcknowledgmentIds.ReportConcerns
        };

        ids.Distinct().Should().HaveCount(ids.Length);
    }

    [Fact]
    public void AllIds_HaveSafetyPrefix()
    {
        SafetyAcknowledgmentIds.Fallibility.Should().StartWith("safety.");
        SafetyAcknowledgmentIds.NotProfessionalAdvice.Should().StartWith("safety.");
        SafetyAcknowledgmentIds.SafetyLockAvailable.Should().StartWith("safety.");
        SafetyAcknowledgmentIds.ReportConcerns.Should().StartWith("safety.");
    }
}

public class OnboardingStepTests
{
    [Theory]
    [InlineData(OnboardingStep.Welcome)]
    [InlineData(OnboardingStep.Capabilities)]
    [InlineData(OnboardingStep.Limitations)]
    [InlineData(OnboardingStep.PrivacySettings)]
    [InlineData(OnboardingStep.SafetyAcknowledgment)]
    [InlineData(OnboardingStep.PersonalitySetup)]
    [InlineData(OnboardingStep.Complete)]
    public void OnboardingStep_AllValuesAreDefined(OnboardingStep step)
    {
        Enum.IsDefined(step).Should().BeTrue();
    }
}
