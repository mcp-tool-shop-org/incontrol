using FluentAssertions;
using InControl.Core.Assistant;
using InControl.Core.Errors;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class ToolRegistryTests
{
    [Fact]
    public void Register_AddsTool()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test-tool");

        registry.Register(tool);

        registry.Tools.Should().Contain(t => t.Id == "test-tool");
    }

    [Fact]
    public void GetTool_ReturnsRegisteredTool()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test-tool");
        registry.Register(tool);

        var retrieved = registry.GetTool("test-tool");

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("test-tool");
    }

    [Fact]
    public void GetTool_ReturnsNull_WhenNotRegistered()
    {
        var registry = new ToolRegistry();

        var retrieved = registry.GetTool("nonexistent");

        retrieved.Should().BeNull();
    }

    [Fact]
    public void Unregister_RemovesTool()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test-tool");
        registry.Register(tool);

        var result = registry.Unregister("test-tool");

        result.Should().BeTrue();
        registry.GetTool("test-tool").Should().BeNull();
    }

    [Fact]
    public void Unregister_ReturnsFalse_WhenNotRegistered()
    {
        var registry = new ToolRegistry();

        var result = registry.Unregister("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public void GetPermission_ReturnsDefaultBasedOnRisk()
    {
        var registry = new ToolRegistry();
        var lowRiskTool = new FakeTool("low", ToolRiskLevel.Low);
        var highRiskTool = new FakeTool("high", ToolRiskLevel.High);

        registry.Register(lowRiskTool);
        registry.Register(highRiskTool);

        registry.GetPermission("low").Should().Be(ToolPermission.AlwaysAllow);
        registry.GetPermission("high").Should().Be(ToolPermission.AlwaysAsk);
    }

    [Fact]
    public void SetPermission_UpdatesPermission()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test-tool", ToolRiskLevel.Low);
        registry.Register(tool);

        registry.SetPermission("test-tool", ToolPermission.AlwaysAsk);

        registry.GetPermission("test-tool").Should().Be(ToolPermission.AlwaysAsk);
    }

    [Fact]
    public void RequiresApproval_ReturnsFalse_ForAlwaysAllow()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test-tool", ToolRiskLevel.Low);
        registry.Register(tool);
        registry.SetPermission("test-tool", ToolPermission.AlwaysAllow);

        var requires = registry.RequiresApproval("test-tool");

        requires.Should().BeFalse();
    }

    [Fact]
    public void RequiresApproval_ReturnsTrue_ForAlwaysAsk()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test-tool", ToolRiskLevel.High);
        registry.Register(tool);

        var requires = registry.RequiresApproval("test-tool");

        requires.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesTool()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test-tool");
        registry.Register(tool);

        var result = await registry.ExecuteAsync(
            "test-tool",
            new Dictionary<string, object?> { ["input"] = "test" }
        );

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("executed");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenToolNotFound()
    {
        var registry = new ToolRegistry();

        var result = await registry.ExecuteAsync(
            "nonexistent",
            new Dictionary<string, object?>()
        );

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_RecordsInAuditLog()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test-tool");
        registry.Register(tool);

        await registry.ExecuteAsync("test-tool", new Dictionary<string, object?>());

        registry.AuditLog.Should().HaveCount(1);
        registry.AuditLog.Single().ToolId.Should().Be("test-tool");
    }

    [Fact]
    public async Task ExecuteAsync_RaisesToolInvokedEvent()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test-tool");
        registry.Register(tool);
        ToolInvokedEventArgs? capturedArgs = null;
        registry.ToolInvoked += (_, args) => capturedArgs = args;

        await registry.ExecuteAsync("test-tool", new Dictionary<string, object?>());

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Record.ToolId.Should().Be("test-tool");
    }

    [Fact]
    public async Task ClearAuditLog_RemovesAllRecords()
    {
        var registry = new ToolRegistry();
        var tool = new FakeTool("test-tool");
        registry.Register(tool);
        await registry.ExecuteAsync("test-tool", new Dictionary<string, object?>());

        registry.ClearAuditLog();

        registry.AuditLog.Should().BeEmpty();
    }

    private sealed class FakeTool : IAssistantTool
    {
        public FakeTool(string id, ToolRiskLevel riskLevel = ToolRiskLevel.Low)
        {
            Id = id;
            RiskLevel = riskLevel;
        }

        public string Id { get; }
        public string Name => $"Fake Tool: {Id}";
        public string Description => "A fake tool for testing";
        public ToolRiskLevel RiskLevel { get; }
        public bool IsReadOnly => true;
        public bool RequiresNetwork => false;
        public IReadOnlyList<ToolParameter> Parameters => [];

        public Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken ct)
        {
            return Task.FromResult(ToolResult.Succeeded($"Tool {Id} executed", TimeSpan.FromMilliseconds(10)));
        }
    }
}

public class ToolRiskLevelTests
{
    [Theory]
    [InlineData(ToolRiskLevel.Low)]
    [InlineData(ToolRiskLevel.Medium)]
    [InlineData(ToolRiskLevel.High)]
    [InlineData(ToolRiskLevel.Critical)]
    public void ToolRiskLevel_AllValuesAreDefined(ToolRiskLevel level)
    {
        Enum.IsDefined(level).Should().BeTrue();
    }
}

public class ToolPermissionTests
{
    [Theory]
    [InlineData(ToolPermission.AlwaysAllow)]
    [InlineData(ToolPermission.AskOnce)]
    [InlineData(ToolPermission.AlwaysAsk)]
    [InlineData(ToolPermission.Disabled)]
    public void ToolPermission_AllValuesAreDefined(ToolPermission permission)
    {
        Enum.IsDefined(permission).Should().BeTrue();
    }
}

public class ToolResultTests
{
    [Fact]
    public void Succeeded_CreatesSuccessResult()
    {
        var result = ToolResult.Succeeded("output", TimeSpan.FromSeconds(1));

        result.Success.Should().BeTrue();
        result.Output.Should().Be("output");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failed_CreatesFailureResult()
    {
        var error = InControlError.Create(ErrorCode.Unknown, "Test error");
        var result = ToolResult.Failed(error, TimeSpan.FromSeconds(1));

        result.Success.Should().BeFalse();
        result.Output.Should().BeNull();
        result.Error.Should().NotBeNull();
    }
}

public class ToolParameterTests
{
    [Fact]
    public void ToolParameter_RecordsAllProperties()
    {
        var param = new ToolParameter(
            Name: "input",
            Description: "The input text",
            Type: ParameterType.String,
            Required: true,
            DefaultValue: "default"
        );

        param.Name.Should().Be("input");
        param.Description.Should().Be("The input text");
        param.Type.Should().Be(ParameterType.String);
        param.Required.Should().BeTrue();
        param.DefaultValue.Should().Be("default");
    }

    [Theory]
    [InlineData(ParameterType.String)]
    [InlineData(ParameterType.Integer)]
    [InlineData(ParameterType.Boolean)]
    [InlineData(ParameterType.FilePath)]
    [InlineData(ParameterType.Url)]
    [InlineData(ParameterType.Json)]
    public void ParameterType_AllValuesAreDefined(ParameterType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }
}
