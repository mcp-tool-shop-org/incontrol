using FluentAssertions;
using InControl.Core.UX;
using InControl.ViewModels.Input;
using Xunit;

namespace InControl.Core.Tests.Input;

public class InputComposerViewModelTests
{
    [Fact]
    public void IntentText_DefaultsToEmpty()
    {
        var vm = new InputComposerViewModel();

        vm.IntentText.Should().BeEmpty();
    }

    [Fact]
    public void IntentText_UpdatesCharacterCount()
    {
        var vm = new InputComposerViewModel();

        vm.IntentText = "Hello";

        vm.CharacterCount.Should().Be("5 characters");
    }

    [Fact]
    public void IntentText_RaisesPropertyChanged()
    {
        var vm = new InputComposerViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        vm.IntentText = "Test";

        changed.Should().Contain(nameof(vm.IntentText));
        changed.Should().Contain(nameof(vm.CharacterCount));
        changed.Should().Contain(nameof(vm.CanRun));
    }

    [Fact]
    public void SelectedModel_DefaultsToNull()
    {
        var vm = new InputComposerViewModel();

        vm.SelectedModel.Should().BeNull();
        vm.HasSelectedModel.Should().BeFalse();
    }

    [Fact]
    public void SelectedModel_UpdatesHasSelectedModel()
    {
        var vm = new InputComposerViewModel();

        vm.SelectedModel = "llama3.2";

        vm.HasSelectedModel.Should().BeTrue();
    }

    [Fact]
    public void SetAvailableModels_PopulatesCollection()
    {
        var vm = new InputComposerViewModel();
        var models = new[] { "llama3.2", "mistral", "phi3" };

        vm.SetAvailableModels(models);

        vm.AvailableModels.Should().HaveCount(3);
        vm.AvailableModels.Should().Contain("llama3.2");
    }

    [Fact]
    public void SetAvailableModels_AutoSelectsFirst()
    {
        var vm = new InputComposerViewModel();
        var models = new[] { "llama3.2", "mistral" };

        vm.SetAvailableModels(models);

        vm.SelectedModel.Should().Be("llama3.2");
    }

    [Fact]
    public void ExecutionState_DefaultsToIdle()
    {
        var vm = new InputComposerViewModel();

        vm.ExecutionState.Should().Be(ExecutionState.Idle);
        vm.IsExecuting.Should().BeFalse();
        vm.AllowsInput.Should().BeTrue();
    }

    [Fact]
    public void ExecutionState_UpdatesIsExecuting()
    {
        var vm = new InputComposerViewModel();

        vm.ExecutionState = ExecutionState.Running;

        vm.IsExecuting.Should().BeTrue();
        vm.AllowsInput.Should().BeFalse();
    }

    [Fact]
    public void ExecutionState_UpdatesCanCancel()
    {
        var vm = new InputComposerViewModel();

        vm.ExecutionState = ExecutionState.Streaming;

        vm.CanCancel.Should().BeTrue();
    }

    [Fact]
    public void ExecutionState_UpdatesButtonVisibility()
    {
        var vm = new InputComposerViewModel();

        vm.ExecutionState = ExecutionState.Running;

        vm.ShowCancelButton.Should().BeTrue();
        vm.ShowRunButton.Should().BeFalse();
    }

    [Fact]
    public void CanRun_RequiresTextAndModel()
    {
        var vm = new InputComposerViewModel();
        vm.SetAvailableModels(new[] { "llama3.2" });

        vm.CanRun.Should().BeFalse(); // No text

        vm.IntentText = "Hello";

        vm.CanRun.Should().BeTrue();
    }

    [Fact]
    public void CanRun_FalseWhenExecuting()
    {
        var vm = new InputComposerViewModel();
        vm.SetAvailableModels(new[] { "llama3.2" });
        vm.IntentText = "Hello";

        vm.ExecutionState = ExecutionState.Running;

        vm.CanRun.Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsStateAndIntent()
    {
        var vm = new InputComposerViewModel();
        vm.IntentText = "Hello";
        vm.ExecutionState = ExecutionState.Complete;
        vm.ElapsedTime = TimeSpan.FromSeconds(5);

        vm.Reset();

        vm.IntentText.Should().BeEmpty();
        vm.ExecutionState.Should().Be(ExecutionState.Idle);
        vm.ElapsedTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ClearIntent_OnlyClearsText()
    {
        var vm = new InputComposerViewModel();
        vm.SetAvailableModels(new[] { "llama3.2" });
        vm.IntentText = "Hello";

        vm.ClearIntent();

        vm.IntentText.Should().BeEmpty();
        vm.SelectedModel.Should().Be("llama3.2");
    }

    [Fact]
    public void ElapsedTime_FormatsCorrectly()
    {
        var vm = new InputComposerViewModel();

        vm.ElapsedTime = TimeSpan.FromMilliseconds(500);
        vm.ElapsedTimeText.Should().Be("< 1s");

        vm.ElapsedTime = TimeSpan.FromSeconds(30);
        vm.ElapsedTimeText.Should().Be("30s");

        vm.ElapsedTime = TimeSpan.FromSeconds(90);
        vm.ElapsedTimeText.Should().Be("1m 30s");
    }

    [Fact]
    public void ExecutionStateText_ReflectsState()
    {
        var vm = new InputComposerViewModel();

        vm.ExecutionState = ExecutionState.LoadingModel;

        vm.ExecutionStateText.Should().Be("Loading model...");
    }
}

public class ContextItemViewModelTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var item = new ContextItemViewModel("test.txt", ContextItemType.File, 1024);

        item.Name.Should().Be("test.txt");
        item.Type.Should().Be(ContextItemType.File);
        item.SizeBytes.Should().Be(1024);
    }

    [Fact]
    public void Id_IsUnique()
    {
        var item1 = new ContextItemViewModel("a.txt", ContextItemType.File);
        var item2 = new ContextItemViewModel("b.txt", ContextItemType.File);

        item1.Id.Should().NotBe(item2.Id);
    }

    [Fact]
    public void SizeText_FormatsCorrectly()
    {
        var item = new ContextItemViewModel("test.txt", ContextItemType.File);

        item.SizeBytes = 500;
        item.SizeText.Should().Be("500 B");

        item.SizeBytes = 2048;
        item.SizeText.Should().Be("2.0 KB");

        item.SizeBytes = 1048576;
        item.SizeText.Should().Be("1.0 MB");
    }

    [Fact]
    public void TypeIcon_VariesByType()
    {
        var file = new ContextItemViewModel("test.txt", ContextItemType.File);
        var output = new ContextItemViewModel("output", ContextItemType.PreviousOutput);
        var image = new ContextItemViewModel("image.png", ContextItemType.Image);
        var code = new ContextItemViewModel("code.py", ContextItemType.Code);

        file.TypeIcon.Should().NotBe(output.TypeIcon);
        output.TypeIcon.Should().NotBe(image.TypeIcon);
        image.TypeIcon.Should().NotBe(code.TypeIcon);
    }
}

public class InputComposerContextItemsTests
{
    [Fact]
    public void ContextItems_InitiallyEmpty()
    {
        var vm = new InputComposerViewModel();

        vm.ContextItems.Should().BeEmpty();
        vm.ContextItemCount.Should().Be(0);
        vm.HasContextItems.Should().BeFalse();
    }

    [Fact]
    public void AddContextItem_IncrementsCount()
    {
        var vm = new InputComposerViewModel();
        var item = new ContextItemViewModel("test.txt", ContextItemType.File);

        vm.AddContextItem(item);

        vm.ContextItems.Should().HaveCount(1);
        vm.ContextItemCount.Should().Be(1);
        vm.HasContextItems.Should().BeTrue();
    }

    [Fact]
    public void RemoveContextItem_DecrementsCount()
    {
        var vm = new InputComposerViewModel();
        var item = new ContextItemViewModel("test.txt", ContextItemType.File);
        vm.AddContextItem(item);

        vm.RemoveContextItem(item);

        vm.ContextItems.Should().BeEmpty();
        vm.ContextItemCount.Should().Be(0);
        vm.HasContextItems.Should().BeFalse();
    }

    [Fact]
    public void ClearContextItems_RemovesAll()
    {
        var vm = new InputComposerViewModel();
        vm.AddContextItem(new ContextItemViewModel("a.txt", ContextItemType.File));
        vm.AddContextItem(new ContextItemViewModel("b.txt", ContextItemType.File));

        vm.ClearContextItems();

        vm.ContextItems.Should().BeEmpty();
        vm.ContextItemCount.Should().Be(0);
    }
}
