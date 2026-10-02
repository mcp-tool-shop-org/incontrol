using FluentAssertions;
using InControl.Core.Assistant;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class MemoryConsentManagerTests
{
    [Fact]
    public void RequestRemember_CreatesPendingRequest()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);

        var request = manager.RequestRemember(
            MemoryType.Preference,
            "response_style",
            "concise",
            "User seems to prefer brief responses"
        );

        request.Should().NotBeNull();
        request.Status.Should().Be(ConsentStatus.Pending);
        manager.PendingRequests.Should().Contain(r => r.Id == request.Id);
    }

    [Fact]
    public void RequestRemember_RaisesConsentRequestedEvent()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);
        MemoryConsentRequestEventArgs? capturedArgs = null;
        manager.ConsentRequested += (_, args) => capturedArgs = args;

        manager.RequestRemember(
            MemoryType.Fact,
            "project_name",
            "InControl",
            "User mentioned this project name"
        );

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Request.Key.Should().Be("project_name");
    }

    [Fact]
    public void Approve_AddsMemoryToStore()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);

        var request = manager.RequestRemember(
            MemoryType.Preference,
            "test_key",
            "test_value",
            "Test justification"
        );

        manager.Approve(request.Id);

        store.Count.Should().Be(1);
        store.All.Single().Key.Should().Be("test_key");
    }

    [Fact]
    public void Approve_RemovesPendingRequest()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);

        var request = manager.RequestRemember(
            MemoryType.Preference,
            "test_key",
            "test_value",
            "Test justification"
        );

        manager.Approve(request.Id);

        manager.PendingRequests.Should().BeEmpty();
    }

    [Fact]
    public void Approve_RaisesConsentDecidedEvent()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);
        ConsentDecisionEventArgs? capturedArgs = null;
        manager.ConsentDecided += (_, args) => capturedArgs = args;

        var request = manager.RequestRemember(
            MemoryType.Preference,
            "test_key",
            "test_value",
            "Test justification"
        );

        manager.Approve(request.Id);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Decision.Should().Be(ConsentDecision.Approved);
        capturedArgs.CreatedMemory.Should().NotBeNull();
    }

    [Fact]
    public void Approve_ReturnsFalse_WhenRequestNotFound()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);

        var result = manager.Approve(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public void Deny_RemovesPendingRequest()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);

        var request = manager.RequestRemember(
            MemoryType.Preference,
            "test_key",
            "test_value",
            "Test justification"
        );

        manager.Deny(request.Id, "Not relevant");

        manager.PendingRequests.Should().BeEmpty();
    }

    [Fact]
    public void Deny_DoesNotAddMemory()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);

        var request = manager.RequestRemember(
            MemoryType.Preference,
            "test_key",
            "test_value",
            "Test justification"
        );

        manager.Deny(request.Id);

        store.Count.Should().Be(0);
    }

    [Fact]
    public void Deny_RaisesConsentDecidedEvent()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);
        ConsentDecisionEventArgs? capturedArgs = null;
        manager.ConsentDecided += (_, args) => capturedArgs = args;

        var request = manager.RequestRemember(
            MemoryType.Preference,
            "test_key",
            "test_value",
            "Test justification"
        );

        manager.Deny(request.Id, "User doesn't want this remembered");

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Decision.Should().Be(ConsentDecision.Denied);
        capturedArgs.CreatedMemory.Should().BeNull();
        capturedArgs.DenialReason.Should().Be("User doesn't want this remembered");
    }

    [Fact]
    public void DismissAll_ClearsPendingRequests()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);

        manager.RequestRemember(MemoryType.Preference, "key1", "value1", "reason1");
        manager.RequestRemember(MemoryType.Preference, "key2", "value2", "reason2");

        var dismissed = manager.DismissAll();

        dismissed.Should().Be(2);
        manager.PendingRequests.Should().BeEmpty();
    }

    [Fact]
    public void RememberExplicit_AddsMemoryImmediately()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);

        var memory = manager.RememberExplicit(
            MemoryType.Instruction,
            "always_use_dark_mode",
            "true"
        );

        store.Count.Should().Be(1);
        memory.Source.Should().Be(MemorySource.ExplicitUser);
        memory.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void RememberExplicit_DoesNotRequireConsent()
    {
        var store = new AssistantMemoryStore();
        var manager = new MemoryConsentManager(store);
        var requestRaised = false;
        manager.ConsentRequested += (_, _) => requestRaised = true;

        manager.RememberExplicit(
            MemoryType.Instruction,
            "test_key",
            "test_value"
        );

        requestRaised.Should().BeFalse();
        manager.PendingRequests.Should().BeEmpty();
    }
}

public class ConfidenceLabelsTests
{
    [Theory]
    [InlineData(1.0, "Confirmed")]
    [InlineData(0.95, "High confidence")]
    [InlineData(0.8, "Likely")]
    [InlineData(0.6, "Possible")]
    [InlineData(0.3, "Uncertain")]
    public void GetLabel_ReturnsCorrectLabel(double confidence, string expectedLabel)
    {
        var label = ConfidenceLabels.GetLabel(confidence);

        label.Should().Be(expectedLabel);
    }

    [Fact]
    public void GetDescription_ReturnsNonEmptyString()
    {
        var description = ConfidenceLabels.GetDescription(0.8);

        description.Should().NotBeNullOrEmpty();
    }
}

public class ConsentStatusTests
{
    [Theory]
    [InlineData(ConsentStatus.Pending)]
    [InlineData(ConsentStatus.Approved)]
    [InlineData(ConsentStatus.Denied)]
    [InlineData(ConsentStatus.Expired)]
    public void ConsentStatus_AllValuesAreDefined(ConsentStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }
}
