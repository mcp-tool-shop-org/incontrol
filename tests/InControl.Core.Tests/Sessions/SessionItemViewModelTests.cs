using FluentAssertions;
using InControl.Core.Models;
using InControl.Core.UX;
using InControl.ViewModels.Sessions;
using Xunit;

namespace InControl.Core.Tests.Sessions;

public class SessionItemViewModelTests
{
    [Fact]
    public void Title_ReturnsConversationTitle()
    {
        var conversation = Conversation.Create("My Session");
        var vm = new SessionItemViewModel(conversation);

        vm.Title.Should().Be("My Session");
    }

    [Fact]
    public void Title_ReturnsUntitledSession_WhenEmpty()
    {
        var conversation = Conversation.Create(""); // Explicitly empty title
        var vm = new SessionItemViewModel(conversation);

        vm.Title.Should().Be(UXStrings.Session.EmptyTitle);
    }

    [Fact]
    public void Id_MatchesConversationId()
    {
        var conversation = Conversation.Create();
        var vm = new SessionItemViewModel(conversation);

        vm.Id.Should().Be(conversation.Id);
    }

    [Fact]
    public void MessageCount_MatchesConversation()
    {
        var conversation = Conversation.Create()
            .WithMessage(Message.User("Hello"))
            .WithMessage(Message.Assistant("Hi there"));
        var vm = new SessionItemViewModel(conversation);

        vm.MessageCount.Should().Be(2);
    }

    [Fact]
    public void IsPinned_DefaultsFalse()
    {
        var conversation = Conversation.Create();
        var vm = new SessionItemViewModel(conversation);

        vm.IsPinned.Should().BeFalse();
    }

    [Fact]
    public void IsPinned_CanBeSet()
    {
        var conversation = Conversation.Create();
        var vm = new SessionItemViewModel(conversation);

        vm.IsPinned = true;

        vm.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void IsSelected_DefaultsFalse()
    {
        var conversation = Conversation.Create();
        var vm = new SessionItemViewModel(conversation);

        vm.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void IsSelected_RaisesPropertyChanged()
    {
        var conversation = Conversation.Create();
        var vm = new SessionItemViewModel(conversation);
        var propertyChanged = false;

        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vm.IsSelected))
                propertyChanged = true;
        };

        vm.IsSelected = true;

        propertyChanged.Should().BeTrue();
    }

    [Fact]
    public void RelativeTime_ReturnsFormattedTime()
    {
        var conversation = Conversation.Create();
        var vm = new SessionItemViewModel(conversation);

        // Just created, should say "Just now"
        vm.RelativeTime.Should().Be("Just now");
    }

    [Fact]
    public void GetConversation_ReturnsUnderlyingConversation()
    {
        var conversation = Conversation.Create("Test");
        var vm = new SessionItemViewModel(conversation);

        vm.GetConversation().Should().BeSameAs(conversation);
    }
}
