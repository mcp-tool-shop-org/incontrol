using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using InControl.ViewModels.Inspector;

namespace InControl.App.Controls;

/// <summary>
/// Right inspector panel with Run and Context tabs.
/// Provides detailed information about execution and context state.
/// </summary>
public sealed partial class InspectorPanel : UserControl
{
    private InspectorViewModel? _viewModel;

    public InspectorPanel()
    {
        this.InitializeComponent();
        SetupEventHandlers();
    }

    /// <summary>
    /// The inspector view model.
    /// </summary>
    public InspectorViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = value;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                ContextItemsList.ItemsSource = _viewModel.ContextItems;
                UpdateTabContent();
                UpdateRunStats();
            }
        }
    }

    /// <summary>
    /// Event raised when close is requested.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Updates the run statistics display.
    /// </summary>
    public void UpdateRunStats(RunStatistics? stats = null)
    {
        var runStats = stats ?? _viewModel?.RunStats ?? new RunStatistics();

        DeviceInfo.Text = runStats.DeviceName;
        ModelInfo.Text = runStats.ModelName;
        LatencyInfo.Text = runStats.LatencyText;
        TokensInInfo.Text = runStats.TokensInText;
        TokensOutInfo.Text = runStats.TokensOutText;
        MemoryInfo.Text = runStats.MemoryText;
    }

    private void SetupEventHandlers()
    {
        RunTab.Checked += (s, e) => SwitchToTab(InspectorTab.Run);
        ContextTab.Checked += (s, e) => SwitchToTab(InspectorTab.Context);
        CloseButton.Click += (s, e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SwitchToTab(InspectorTab tab)
    {
        if (_viewModel != null)
        {
            _viewModel.ActiveTab = tab;
        }
        UpdateTabContent();
    }

    private void UpdateTabContent()
    {
        var showRun = _viewModel?.ShowRunTab ?? true;

        RunContent.Visibility = showRun ? Visibility.Visible : Visibility.Collapsed;
        ContextContent.Visibility = showRun ? Visibility.Collapsed : Visibility.Visible;

        // Update context items visibility
        if (!showRun)
        {
            UpdateContextItemsVisibility();
        }
    }

    private void UpdateContextItemsVisibility()
    {
        var hasItems = _viewModel?.HasContextItems ?? false;
        ContextEmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        ContextItemsList.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(InspectorViewModel.ActiveTab):
                    UpdateTabContent();
                    break;
                case nameof(InspectorViewModel.HasContextItems):
                    UpdateContextItemsVisibility();
                    break;
                case nameof(InspectorViewModel.RunStats):
                    UpdateRunStats();
                    break;
            }
        });
    }
}
