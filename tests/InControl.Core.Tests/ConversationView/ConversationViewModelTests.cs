using FluentAssertions;
using InControl.Core.Models;
using InControl.Core.UX;
using InControl.ViewModels.ConversationView;
using Xunit;

namespace InControl.Core.Tests.ConversationView;

public class ConversationViewModelTests
{
    [Fact]
    public void Messages_InitiallyEmpty()
    {
        var vm = new ConversationViewModel();

        vm.Messages.Should().BeEmpty();
        vm.HasMessages.Should().BeFalse();
    }

    [Fact]
    public void ViewState_DefaultsToWelcome()
    {
        var vm = new ConversationViewModel();

        vm.ViewState.Should().Be(ConversationViewState.Welcome);
        vm.ShowWelcome.Should().BeTrue();
        vm.ShowEmptySession.Should().BeFalse();
        vm.ShowMessages.Should().BeFalse();
    }

    [Fact]
    public void LoadConversation_ShowsEmptySession_WhenNoMessages()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test");

        vm.LoadConversation(conversation);

        vm.ViewState.Should().Be(ConversationViewState.EmptySession);
        vm.ShowEmptySession.Should().BeTrue();
    }

    [Fact]
    public void LoadConversation_ShowsMessages_WhenHasMessages()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test")
            .WithMessage(Message.User("Hello"));

        vm.LoadConversation(conversation);

        vm.ViewState.Should().Be(ConversationViewState.Messages);
        vm.ShowMessages.Should().BeTrue();
        vm.Messages.Should().HaveCount(1);
    }

    [Fact]
    public void LoadConversation_UpdatesSessionTitle()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("My Session");

        vm.LoadConversation(conversation);

        vm.SessionTitle.Should().Be("My Session");
    }

    [Fact]
    public void ClearConversation_ResetsToWelcome()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test")
            .WithMessage(Message.User("Hello"));
        vm.LoadConversation(conversation);

        vm.ClearConversation();

        vm.ViewState.Should().Be(ConversationViewState.Welcome);
        vm.Messages.Should().BeEmpty();
    }

    [Fact]
    public void AddUserIntent_AddsMessage()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test");
        vm.LoadConversation(conversation);

        vm.AddUserIntent("Hello world");

        vm.Messages.Should().HaveCount(1);
        vm.Messages[0].Content.Should().Be("Hello world");
        vm.Messages[0].IsUser.Should().BeTrue();
    }

    [Fact]
    public void AddUserIntent_ChangesViewStateToMessages()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test");
        vm.LoadConversation(conversation);

        vm.AddUserIntent("Test");

        vm.ViewState.Should().Be(ConversationViewState.Messages);
    }

    [Fact]
    public void BeginModelOutput_AddsStreamingPlaceholder()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test");
        vm.LoadConversation(conversation);

        vm.BeginModelOutput("llama3.2");

        vm.Messages.Should().HaveCount(1);
        vm.Messages[0].IsStreaming.Should().BeTrue();
        vm.Messages[0].Model.Should().Be("llama3.2");
    }

    [Fact]
    public void AppendToModelOutput_UpdatesStreamingContent()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test");
        vm.LoadConversation(conversation);
        vm.BeginModelOutput();

        vm.AppendToModelOutput("Hello ");
        vm.AppendToModelOutput("world");

        vm.Messages[0].Content.Should().Be("Hello world");
    }

    [Fact]
    public void CompleteModelOutput_ReplacesStreamingMessage()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test");
        vm.LoadConversation(conversation);
        vm.BeginModelOutput();
        vm.AppendToModelOutput("Streaming...");

        var finalMessage = Message.Assistant("Final content", "llama3.2");
        vm.CompleteModelOutput(finalMessage);

        vm.Messages.Should().HaveCount(1);
        vm.Messages[0].Content.Should().Be("Final content");
        vm.Messages[0].IsStreaming.Should().BeFalse();
    }

    [Fact]
    public void CancelModelOutput_RemovesStreamingMessage()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test");
        vm.LoadConversation(conversation);
        vm.BeginModelOutput();

        vm.CancelModelOutput();

        vm.Messages.Should().BeEmpty();
    }

    [Fact]
    public void ExecutionState_DefaultsToIdle()
    {
        var vm = new ConversationViewModel();

        vm.ExecutionState.Should().Be(ExecutionState.Idle);
        vm.IsExecuting.Should().BeFalse();
        vm.ShowExecutionIndicator.Should().BeFalse();
    }

    [Fact]
    public void ExecutionState_UpdatesIsExecuting()
    {
        var vm = new ConversationViewModel();

        vm.ExecutionState = ExecutionState.Running;

        vm.IsExecuting.Should().BeTrue();
        vm.ShowExecutionIndicator.Should().BeTrue();
    }

    [Fact]
    public void ExecutionStateText_ReflectsState()
    {
        var vm = new ConversationViewModel();

        vm.ExecutionState = ExecutionState.Streaming;

        vm.ExecutionStateText.Should().Be("Receiving output...");
    }

    [Fact]
    public void ElapsedTime_FormatsCorrectly()
    {
        var vm = new ConversationViewModel();

        vm.ElapsedTime = TimeSpan.FromSeconds(45);
        vm.ElapsedTimeText.Should().Be("45s");

        vm.ElapsedTime = TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(30));
        vm.ElapsedTimeText.Should().Be("2m 30s");
    }

    [Fact]
    public void CurrentModel_CanBeSet()
    {
        var vm = new ConversationViewModel();

        vm.CurrentModel = "llama3.2";

        vm.CurrentModel.Should().Be("llama3.2");
    }

    [Fact]
    public void GetConversation_ReturnsLoadedConversation()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test");
        vm.LoadConversation(conversation);

        vm.GetConversation().Should().BeSameAs(conversation);
    }

    [Fact]
    public void GetConversation_ReturnsNull_WhenNotLoaded()
    {
        var vm = new ConversationViewModel();

        vm.GetConversation().Should().BeNull();
    }
}

public class ConversationViewStateTests
{
    [Fact]
    public void PropertyChanged_RaisedForViewState()
    {
        var vm = new ConversationViewModel();
        var conversation = Core.Models.Conversation.Create("Test");
        var changed = new List<string>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        vm.LoadConversation(conversation);

        changed.Should().Contain(nameof(vm.ViewState));
        changed.Should().Contain(nameof(vm.ShowWelcome));
        changed.Should().Contain(nameof(vm.ShowEmptySession));
        changed.Should().Contain(nameof(vm.ShowMessages));
    }

    [Fact]
    public void PropertyChanged_RaisedForExecutionState()
    {
        var vm = new ConversationViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        vm.ExecutionState = ExecutionState.Running;

        changed.Should().Contain(nameof(vm.ExecutionState));
        changed.Should().Contain(nameof(vm.IsExecuting));
        changed.Should().Contain(nameof(vm.ShowExecutionIndicator));
    }
}
