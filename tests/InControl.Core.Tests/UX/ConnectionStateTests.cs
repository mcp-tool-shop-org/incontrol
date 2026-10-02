using FluentAssertions;
using InControl.Core.UX;
using Xunit;

namespace InControl.Core.Tests.UX;

public class ConnectionStateTests
{
    [Theory]
    [InlineData(ConnectionState.Unknown, "Checking connection...")]
    [InlineData(ConnectionState.Connecting, "Connecting...")]
    [InlineData(ConnectionState.Connected, "Connected")]
    [InlineData(ConnectionState.Disconnected, "Disconnected")]
    [InlineData(ConnectionState.Timeout, "Connection timeout")]
    [InlineData(ConnectionState.Degraded, "Connected (degraded)")]
    public void ToDisplayText_ReturnsCorrectText(ConnectionState state, string expected)
    {
        state.ToDisplayText().Should().Be(expected);
    }

    [Theory]
    [InlineData(ConnectionState.Connected, true)]
    [InlineData(ConnectionState.Degraded, true)]
    [InlineData(ConnectionState.Disconnected, false)]
    [InlineData(ConnectionState.Timeout, false)]
    [InlineData(ConnectionState.Unknown, false)]
    public void IsUsable_IdentifiesUsableStates(ConnectionState state, bool expected)
    {
        state.IsUsable().Should().Be(expected);
    }

    [Theory]
    [InlineData(ConnectionState.Unknown, true)]
    [InlineData(ConnectionState.Connecting, true)]
    [InlineData(ConnectionState.Connected, false)]
    [InlineData(ConnectionState.Disconnected, false)]
    public void IsConnecting_IdentifiesConnectingStates(ConnectionState state, bool expected)
    {
        state.IsConnecting().Should().Be(expected);
    }
}
