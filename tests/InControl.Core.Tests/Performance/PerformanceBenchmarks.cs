using System.Diagnostics;
using FluentAssertions;
using InControl.Core.Models;
using InControl.Core.State;
using Xunit;

namespace InControl.Core.Tests.Performance;

/// <summary>
/// Basic performance benchmarks for Phase 4 Gate 5.
/// These are not comprehensive benchmarks but provide baseline measurements.
/// </summary>
public class PerformanceBenchmarks
{
    [Fact]
    public void AppState_Creation_IsUnderTarget()
    {
        // Target: Initial state creation should be fast
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 1000; i++)
        {
            _ = AppState.Initial();
        }

        sw.Stop();

        // 1000 iterations should complete in under 100ms
        sw.ElapsedMilliseconds.Should().BeLessThan(100,
            "AppState.Initial() should be very fast");
    }

    [Fact]
    public void Conversation_Creation_IsUnderTarget()
    {
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 1000; i++)
        {
            _ = Conversation.Create($"Test {i}");
        }

        sw.Stop();

        // 1000 iterations should complete in under 100ms
        sw.ElapsedMilliseconds.Should().BeLessThan(100,
            "Conversation.Create() should be very fast");
    }

    [Fact]
    public void Message_Creation_IsUnderTarget()
    {
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 10000; i++)
        {
            _ = Message.User($"Test message {i}");
        }

        sw.Stop();

        // 10000 iterations should complete in under 100ms
        sw.ElapsedMilliseconds.Should().BeLessThan(100,
            "Message.User() should be very fast");
    }

    [Fact]
    public void Conversation_With1000Messages_IsResponsive()
    {
        var conversation = Conversation.Create("Performance Test");

        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 1000; i++)
        {
            conversation = conversation.WithMessage(Message.User($"Message {i}"));
        }

        sw.Stop();

        // Building a conversation with 1000 messages should be fast
        sw.ElapsedMilliseconds.Should().BeLessThan(500,
            "Adding 1000 messages should complete in under 500ms");

        conversation.Messages.Should().HaveCount(1000);
    }

    [Fact]
    public void StateSerializer_Serialize_1000Messages_IsUnderTarget()
    {
        var conversation = Conversation.Create("Serialize Test");
        for (var i = 0; i < 1000; i++)
        {
            conversation = conversation.WithMessage(Message.User($"Message {i} with some content"));
        }

        var state = AppState.Initial().WithConversation(conversation);

        var sw = Stopwatch.StartNew();
        var json = StateSerializer.Serialize(state);
        sw.Stop();

        // Serializing 1000 messages should be fast
        sw.ElapsedMilliseconds.Should().BeLessThan(200,
            "Serializing 1000 messages should complete in under 200ms");

        json.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void StateSerializer_Deserialize_1000Messages_IsUnderTarget()
    {
        var conversation = Conversation.Create("Deserialize Test");
        for (var i = 0; i < 1000; i++)
        {
            conversation = conversation.WithMessage(Message.User($"Message {i} with some content"));
        }

        var state = AppState.Initial().WithConversation(conversation);
        var json = StateSerializer.Serialize(state);

        var sw = Stopwatch.StartNew();
        var result = StateSerializer.Deserialize<AppState>(json);
        sw.Stop();

        // Deserializing 1000 messages should be fast
        sw.ElapsedMilliseconds.Should().BeLessThan(200,
            "Deserializing 1000 messages should complete in under 200ms");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AppState_SessionSwitch_50Sessions_IsUnderTarget()
    {
        // Create state with 50 conversations
        var state = AppState.Initial();
        for (var i = 0; i < 50; i++)
        {
            var conv = Conversation.Create($"Session {i}");
            state = state.WithConversation(conv);
        }

        // Measure session switch (setting active conversation)
        var conversations = state.Conversations.ToList();

        var sw = Stopwatch.StartNew();

        // Switch between all 50 sessions
        foreach (var conv in conversations)
        {
            state = state.WithActiveConversation(conv.Id);
        }

        sw.Stop();

        // Target: < 100ms for switching between 50 sessions
        sw.ElapsedMilliseconds.Should().BeLessThan(100,
            "Session switch for 50 sessions should be under 100ms");
    }

    [Fact]
    public void MemoryUsage_LargeConversation_IsReasonable()
    {
        // Baseline memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var baseMemory = GC.GetTotalMemory(true);

        // Create conversation with 10k messages
        var conversation = Conversation.Create("Memory Test");
        for (var i = 0; i < 10000; i++)
        {
            conversation = conversation.WithMessage(
                Message.User($"This is test message number {i} with some typical content length."));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var afterMemory = GC.GetTotalMemory(true);

        var usedMemory = afterMemory - baseMemory;

        // 10k messages with ~60 chars each should use reasonable memory
        // Very rough estimate: < 100MB for 10k messages
        usedMemory.Should().BeLessThan(100 * 1024 * 1024,
            "10k messages should use less than 100MB of memory");

        conversation.Messages.Should().HaveCount(10000);
    }
}
