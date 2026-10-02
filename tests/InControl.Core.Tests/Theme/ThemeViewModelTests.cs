using FluentAssertions;
using InControl.ViewModels.Theme;
using Xunit;

namespace InControl.Core.Tests.Theme;

public class ThemeViewModelTests
{
    [Fact]
    public void Theme_DefaultsToSystem()
    {
        var vm = new ThemeViewModel();

        vm.Theme.Should().Be(AppTheme.System);
        vm.ThemeDisplayName.Should().Be("Follow system");
    }

    [Fact]
    public void ThemeDisplayName_VariesByTheme()
    {
        var vm = new ThemeViewModel();

        vm.Theme = AppTheme.Light;
        vm.ThemeDisplayName.Should().Be("Light");

        vm.Theme = AppTheme.Dark;
        vm.ThemeDisplayName.Should().Be("Dark");
    }

    [Fact]
    public void IsDarkMode_ReflectsTheme()
    {
        var vm = new ThemeViewModel();

        vm.Theme = AppTheme.Light;
        vm.IsDarkMode.Should().BeFalse();

        vm.Theme = AppTheme.Dark;
        vm.IsDarkMode.Should().BeTrue();
    }

    [Fact]
    public void AccentColor_DefaultsToSystem()
    {
        var vm = new ThemeViewModel();

        vm.AccentColor.Should().Be(AccentColor.System);
        vm.AccentColorDisplayName.Should().Be("System accent");
    }

    [Fact]
    public void AccentColorDisplayName_VariesByColor()
    {
        var vm = new ThemeViewModel();

        vm.AccentColor = AccentColor.Blue;
        vm.AccentColorDisplayName.Should().Be("Blue");

        vm.AccentColor = AccentColor.Purple;
        vm.AccentColorDisplayName.Should().Be("Purple");

        vm.AccentColor = AccentColor.Teal;
        vm.AccentColorDisplayName.Should().Be("Teal");
    }

    [Fact]
    public void UseCompactMode_DefaultsToFalse()
    {
        var vm = new ThemeViewModel();

        vm.UseCompactMode.Should().BeFalse();
        vm.DensityDisplayName.Should().Be("Comfortable");
    }

    [Fact]
    public void DensityDisplayName_VariesByCompactMode()
    {
        var vm = new ThemeViewModel();

        vm.UseCompactMode = true;
        vm.DensityDisplayName.Should().Be("Compact");

        vm.UseCompactMode = false;
        vm.DensityDisplayName.Should().Be("Comfortable");
    }

    [Fact]
    public void ShowAnimations_DefaultsToTrue()
    {
        var vm = new ThemeViewModel();

        vm.ShowAnimations.Should().BeTrue();
    }

    [Fact]
    public void UIScale_DefaultsToOne()
    {
        var vm = new ThemeViewModel();

        vm.UIScale.Should().Be(1.0);
        vm.UIScalePercent.Should().Be("100%");
    }

    [Fact]
    public void UIScale_ClampsBetweenLimits()
    {
        var vm = new ThemeViewModel();

        vm.UIScale = 0.5; // Below minimum
        vm.UIScale.Should().Be(0.75);

        vm.UIScale = 2.0; // Above maximum
        vm.UIScale.Should().Be(1.5);
    }

    [Fact]
    public void UIScalePercent_FormatsCorrectly()
    {
        var vm = new ThemeViewModel();

        vm.UIScale = 1.25;
        vm.UIScalePercent.Should().Be("125%");

        vm.UIScale = 0.75;
        vm.UIScalePercent.Should().Be("75%");
    }

    [Fact]
    public void ResetToDefaults_ResetsAllSettings()
    {
        var vm = new ThemeViewModel();
        vm.Theme = AppTheme.Dark;
        vm.AccentColor = AccentColor.Purple;
        vm.UseCompactMode = true;
        vm.ShowAnimations = false;
        vm.UIScale = 1.25;

        vm.ResetToDefaults();

        vm.Theme.Should().Be(AppTheme.System);
        vm.AccentColor.Should().Be(AccentColor.System);
        vm.UseCompactMode.Should().BeFalse();
        vm.ShowAnimations.Should().BeTrue();
        vm.UIScale.Should().Be(1.0);
    }

    [Fact]
    public void PropertyChanged_RaisedForTheme()
    {
        var vm = new ThemeViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        vm.Theme = AppTheme.Dark;

        changed.Should().Contain(nameof(vm.Theme));
        changed.Should().Contain(nameof(vm.ThemeDisplayName));
        changed.Should().Contain(nameof(vm.IsDarkMode));
    }

    [Fact]
    public void PropertyChanged_RaisedForAccentColor()
    {
        var vm = new ThemeViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        vm.AccentColor = AccentColor.Teal;

        changed.Should().Contain(nameof(vm.AccentColor));
        changed.Should().Contain(nameof(vm.AccentColorDisplayName));
    }

    [Fact]
    public void PropertyChanged_RaisedForUIScale()
    {
        var vm = new ThemeViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        vm.UIScale = 1.25;

        changed.Should().Contain(nameof(vm.UIScale));
        changed.Should().Contain(nameof(vm.UIScalePercent));
    }
}
