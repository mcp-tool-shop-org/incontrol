using FluentAssertions;
using InControl.Core.Models;
using InControl.ViewModels.Sessions;
using Xunit;

namespace InControl.Core.Tests.Sessions;

public class SessionListViewModelTests
{
    [Fact]
    public void Constructor_InitializesEmptyCollections()
    {
        var vm = new SessionListViewModel();

        vm.Sessions.Should().BeEmpty();
        vm.PinnedSessions.Should().BeEmpty();
        vm.FilteredSessions.Should().BeEmpty();
    }

    [Fact]
    public void HasSessions_ReturnsFalse_WhenEmpty()
    {
        var vm = new SessionListViewModel();

        vm.HasSessions.Should().BeFalse();
    }

    [Fact]
    public void CreateSession_AddsToSessionsCollection()
    {
        var vm = new SessionListViewModel();

        var session = vm.CreateSession();

        vm.Sessions.Should().Contain(session);
        vm.HasSessions.Should().BeTrue();
    }

    [Fact]
    public void CreateSession_SelectsNewSession()
    {
        var vm = new SessionListViewModel();

        var session = vm.CreateSession();

        vm.SelectedSession.Should().Be(session);
        session.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void CreateSession_InsertsAtTop()
    {
        var vm = new SessionListViewModel();
        vm.CreateSession();
        var second = vm.CreateSession();

        vm.Sessions[0].Should().Be(second);
    }

    [Fact]
    public void AddSession_AddsToCorrectCollection()
    {
        var vm = new SessionListViewModel();
        var conversation = Conversation.Create("Test");

        vm.AddSession(conversation, isPinned: false);

        vm.Sessions.Should().HaveCount(1);
        vm.PinnedSessions.Should().BeEmpty();
    }

    [Fact]
    public void AddSession_Pinned_AddsTooPinnedCollection()
    {
        var vm = new SessionListViewModel();
        var conversation = Conversation.Create("Pinned");

        vm.AddSession(conversation, isPinned: true);

        vm.PinnedSessions.Should().HaveCount(1);
        vm.Sessions.Should().BeEmpty();
        vm.HasPinnedSessions.Should().BeTrue();
    }

    [Fact]
    public void RemoveSession_RemovesFromCollection()
    {
        var vm = new SessionListViewModel();
        var session = vm.CreateSession();

        vm.RemoveSession(session);

        vm.Sessions.Should().NotContain(session);
        vm.HasSessions.Should().BeFalse();
    }

    [Fact]
    public void RemoveSession_ClearsSelection_WhenSelectedRemoved()
    {
        var vm = new SessionListViewModel();
        var session = vm.CreateSession();

        vm.RemoveSession(session);

        vm.SelectedSession.Should().BeNull();
    }

    [Fact]
    public void TogglePin_PinsSession()
    {
        var vm = new SessionListViewModel();
        var session = vm.CreateSession();

        vm.TogglePin(session);

        session.IsPinned.Should().BeTrue();
        vm.PinnedSessions.Should().Contain(session);
        vm.Sessions.Should().NotContain(session);
    }

    [Fact]
    public void TogglePin_UnpinsSession()
    {
        var vm = new SessionListViewModel();
        vm.AddSession(Conversation.Create("Pinned"), isPinned: true);
        var pinned = vm.PinnedSessions[0];

        vm.TogglePin(pinned);

        pinned.IsPinned.Should().BeFalse();
        vm.Sessions.Should().Contain(pinned);
        vm.PinnedSessions.Should().NotContain(pinned);
    }

    [Fact]
    public void DuplicateSession_CreatesNewSession()
    {
        var vm = new SessionListViewModel();
        var original = vm.CreateSession();

        var duplicate = vm.DuplicateSession(original);

        duplicate.Should().NotBeSameAs(original);
        duplicate.Id.Should().NotBe(original.Id);
        vm.Sessions.Should().HaveCount(2);
    }

    [Fact]
    public void DuplicateSession_CopiesTitle()
    {
        var vm = new SessionListViewModel();
        vm.AddSession(Conversation.Create("My Session"));
        var original = vm.Sessions[0];

        var duplicate = vm.DuplicateSession(original);

        duplicate.Title.Should().Contain("My Session");
        duplicate.Title.Should().Contain("copy");
    }

    [Fact]
    public void SearchQuery_FiltersResults()
    {
        var vm = new SessionListViewModel();
        vm.AddSession(Conversation.Create("Alpha Session"));
        vm.AddSession(Conversation.Create("Beta Session"));
        vm.AddSession(Conversation.Create("Gamma"));

        vm.SearchQuery = "Session";

        vm.FilteredSessions.Should().HaveCount(2);
        vm.FilteredSessions.Should().OnlyContain(s => s.Title.Contains("Session"));
    }

    [Fact]
    public void SearchQuery_IsCaseInsensitive()
    {
        var vm = new SessionListViewModel();
        vm.AddSession(Conversation.Create("Alpha Session"));

        vm.SearchQuery = "ALPHA";

        vm.FilteredSessions.Should().HaveCount(1);
    }

    [Fact]
    public void ClearSearch_ShowsAllSessions()
    {
        var vm = new SessionListViewModel();
        vm.AddSession(Conversation.Create("Alpha"));
        vm.AddSession(Conversation.Create("Beta"));
        vm.SearchQuery = "Alpha";

        vm.ClearSearch();

        vm.SearchQuery.Should().BeEmpty();
        vm.FilteredSessions.Should().HaveCount(2);
    }

    [Fact]
    public void SelectedSession_DeselectsPrevious()
    {
        var vm = new SessionListViewModel();
        var first = vm.CreateSession();
        var second = vm.CreateSession();

        vm.SelectedSession = first;
        vm.SelectedSession = second;

        first.IsSelected.Should().BeFalse();
        second.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void HasSelectedSession_ReflectsSelection()
    {
        var vm = new SessionListViewModel();

        vm.HasSelectedSession.Should().BeFalse();

        var session = vm.CreateSession();
        vm.SelectedSession = session;

        vm.HasSelectedSession.Should().BeTrue();
    }
}
