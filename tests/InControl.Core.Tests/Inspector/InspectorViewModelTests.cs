using FluentAssertions;
using InControl.Core.UX;
using InControl.ViewModels.Input;
using InControl.ViewModels.Inspector;
using Xunit;

namespace InControl.Core.Tests.Inspector;

public class InspectorViewModelTests
{
    [Fact]
    public void ActiveTab_DefaultsToRun()
    {
        var vm = new InspectorViewModel();

        vm.ActiveTab.Should().Be(InspectorTab.Run);
        vm.ShowRunTab.Should().BeTrue();
        vm.ShowContextTab.Should().BeFalse();
    }

    [Fact]
    public void ActiveTab_SwitchesVisibility()
    {
        var vm = new InspectorViewModel();

        vm.ActiveTab = InspectorTab.Context;

        vm.ShowRunTab.Should().BeFalse();
        vm.ShowContextTab.Should().BeTrue();
    }

    [Fact]
    public void IsVisible_DefaultsToTrue()
    {
        var vm = new InspectorViewModel();

        vm.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void ToggleVisibility_TogglesIsVisible()
    {
        var vm = new InspectorViewModel();

        vm.ToggleVisibility();
        vm.IsVisible.Should().BeFalse();

        vm.ToggleVisibility();
        vm.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void ContextItems_InitiallyEmpty()
    {
        var vm = new InspectorViewModel();

        vm.ContextItems.Should().BeEmpty();
        vm.HasContextItems.Should().BeFalse();
    }

    [Fact]
    public void AddContextItem_AddsToCollection()
    {
        var vm = new InspectorViewModel();
        var item = new ContextItemViewModel("test.txt", ContextItemType.File);

        vm.AddContextItem(item);

        vm.ContextItems.Should().HaveCount(1);
        vm.HasContextItems.Should().BeTrue();
    }

    [Fact]
    public void RemoveContextItem_RemovesFromCollection()
    {
        var vm = new InspectorViewModel();
        var item = new ContextItemViewModel("test.txt", ContextItemType.File);
        vm.AddContextItem(item);

        vm.RemoveContextItem(item);

        vm.ContextItems.Should().BeEmpty();
        vm.HasContextItems.Should().BeFalse();
    }

    [Fact]
    public void ClearContextItems_RemovesAll()
    {
        var vm = new InspectorViewModel();
        vm.AddContextItem(new ContextItemViewModel("a.txt", ContextItemType.File));
        vm.AddContextItem(new ContextItemViewModel("b.txt", ContextItemType.File));

        vm.ClearContextItems();

        vm.ContextItems.Should().BeEmpty();
    }

    [Fact]
    public void UpdateRunStats_SetsStats()
    {
        var vm = new InspectorViewModel();
        var stats = new RunStatistics { DeviceName = "RTX 5080" };

        vm.UpdateRunStats(stats);

        vm.RunStats.DeviceName.Should().Be("RTX 5080");
    }

    [Fact]
    public void ClearRunStats_ResetStats()
    {
        var vm = new InspectorViewModel();
        vm.UpdateRunStats(new RunStatistics { DeviceName = "Test" });

        vm.ClearRunStats();

        vm.RunStats.DeviceName.Should().Be("—");
    }

    [Fact]
    public void PropertyChanged_RaisedForActiveTab()
    {
        var vm = new InspectorViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        vm.ActiveTab = InspectorTab.Context;

        changed.Should().Contain(nameof(vm.ActiveTab));
        changed.Should().Contain(nameof(vm.ShowRunTab));
        changed.Should().Contain(nameof(vm.ShowContextTab));
    }
}

public class RunStatisticsTests
{
    [Fact]
    public void DeviceName_DefaultsToDash()
    {
        var stats = new RunStatistics();

        stats.DeviceName.Should().Be("—");
    }

    [Fact]
    public void LatencyText_FormatsCorrectly()
    {
        var stats = new RunStatistics();

        stats.Latency = TimeSpan.Zero;
        stats.LatencyText.Should().Be("—");

        stats.Latency = TimeSpan.FromMilliseconds(250);
        stats.LatencyText.Should().Be("250ms");

        stats.Latency = TimeSpan.FromSeconds(1.5);
        stats.LatencyText.Should().Be("1.50s");
    }

    [Fact]
    public void TokensInText_FormatsCorrectly()
    {
        var stats = new RunStatistics();

        stats.TokensIn = 0;
        stats.TokensInText.Should().Be("—");

        stats.TokensIn = 1234;
        stats.TokensInText.Should().Be("1,234");
    }

    [Fact]
    public void TokensOutText_FormatsCorrectly()
    {
        var stats = new RunStatistics();

        stats.TokensOut = 0;
        stats.TokensOutText.Should().Be("—");

        stats.TokensOut = 5678;
        stats.TokensOutText.Should().Be("5,678");
    }

    [Fact]
    public void MemoryText_FormatsCorrectly()
    {
        var stats = new RunStatistics();

        stats.MemoryUsedBytes = 0;
        stats.MemoryText.Should().Be("—");

        stats.MemoryUsedBytes = 512;
        stats.MemoryText.Should().Contain("KB");

        stats.MemoryUsedBytes = 1024 * 1024 * 512; // 512 MB
        stats.MemoryText.Should().Contain("MB");

        stats.MemoryUsedBytes = (long)(1024 * 1024 * 1024 * 2.5); // 2.5 GB
        stats.MemoryText.Should().Contain("GB");
    }

    [Fact]
    public void StateText_ReflectsState()
    {
        var stats = new RunStatistics();

        stats.State = ExecutionState.Running;

        stats.StateText.Should().Be("Running inference...");
    }

    [Fact]
    public void TokensPerSecond_CalculatesCorrectly()
    {
        var stats = new RunStatistics
        {
            TokensOut = 100,
            Latency = TimeSpan.FromSeconds(2)
        };

        stats.TokensPerSecond.Should().Be(50);
    }

    [Fact]
    public void TokensPerSecond_ReturnsZero_WhenNoData()
    {
        var stats = new RunStatistics();

        stats.TokensPerSecond.Should().Be(0);
    }

    [Fact]
    public void PropertyChanged_RaisedForLatency()
    {
        var stats = new RunStatistics();
        var changed = new List<string>();
        stats.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        stats.Latency = TimeSpan.FromSeconds(1);

        changed.Should().Contain(nameof(stats.Latency));
        changed.Should().Contain(nameof(stats.LatencyText));
    }
}
