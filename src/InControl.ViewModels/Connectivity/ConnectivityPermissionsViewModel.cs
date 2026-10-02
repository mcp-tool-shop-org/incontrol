using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using InControl.Core.Assistant;

namespace InControl.ViewModels.Connectivity;

/// <summary>
/// ViewModel for managing internet endpoint permissions.
/// Provides operator control over which endpoints are allowed.
/// </summary>
public partial class ConnectivityPermissionsViewModel : ViewModelBase
{
    private readonly InternetToolPermissions _permissions;

    /// <summary>
    /// All configured permission rules.
    /// </summary>
    public ObservableCollection<PermissionRuleViewModel> Rules { get; } = [];

    /// <summary>
    /// Pending approval requests from the assistant.
    /// </summary>
    public ObservableCollection<PermissionRequestViewModel> PendingRequests { get; } = [];

    /// <summary>
    /// Whether there are pending requests.
    /// </summary>
    public bool HasPendingRequests => PendingRequests.Count > 0;

    /// <summary>
    /// New rule endpoint pattern being entered.
    /// </summary>
    [ObservableProperty]
    private string _newEndpointPattern = string.Empty;

    /// <summary>
    /// New rule permission level.
    /// </summary>
    [ObservableProperty]
    private ToolPermission _newPermission = ToolPermission.AlwaysAllow;

    /// <summary>
    /// New rule notes.
    /// </summary>
    [ObservableProperty]
    private string _newNotes = string.Empty;

    /// <summary>
    /// Available permission levels.
    /// </summary>
    public static IReadOnlyList<PermissionOption> AvailablePermissions { get; } =
    [
        new(ToolPermission.AlwaysAllow, "Always Allow", "Automatically approve requests to this endpoint"),
        new(ToolPermission.AlwaysAsk, "Ask Every Time", "Prompt for approval each time"),
        new(ToolPermission.Disabled, "Deny", "Block all requests to this endpoint")
    ];

    public ConnectivityPermissionsViewModel(
        InternetToolPermissions permissions,
        ILogger<ConnectivityPermissionsViewModel> logger)
        : base(logger)
    {
        _permissions = permissions;

        // Subscribe to permission requests
        _permissions.PermissionRequested += OnPermissionRequested;

        // Load existing rules
        RefreshRules();
    }

    /// <summary>
    /// Refreshes the rules list from the permissions manager.
    /// </summary>
    private void RefreshRules()
    {
        var rules = _permissions.GetRules();

        Rules.Clear();
        foreach (var (pattern, rule) in rules)
        {
            Rules.Add(new PermissionRuleViewModel(pattern, rule.Permission, rule.Notes));
        }
    }

    /// <summary>
    /// Adds a new permission rule.
    /// </summary>
    [RelayCommand]
    private void AddRule()
    {
        if (string.IsNullOrWhiteSpace(NewEndpointPattern))
        {
            SetError("Endpoint pattern is required");
            return;
        }

        _permissions.SetRule(NewEndpointPattern, NewPermission, NewNotes);

        Logger.LogInformation(
            "Added permission rule: {Pattern} = {Permission}",
            NewEndpointPattern, NewPermission);

        // Reset form
        NewEndpointPattern = string.Empty;
        NewNotes = string.Empty;
        NewPermission = ToolPermission.AlwaysAllow;

        RefreshRules();
    }

    /// <summary>
    /// Removes a permission rule.
    /// </summary>
    [RelayCommand]
    private void RemoveRule(string endpointPattern)
    {
        _permissions.RemoveRule(endpointPattern);
        Logger.LogInformation("Removed permission rule: {Pattern}", endpointPattern);
        RefreshRules();
    }

    /// <summary>
    /// Updates an existing rule's permission.
    /// </summary>
    [RelayCommand]
    private void UpdateRule(PermissionRuleViewModel rule)
    {
        _permissions.SetRule(rule.EndpointPattern, rule.Permission, rule.Notes);
        Logger.LogInformation(
            "Updated permission rule: {Pattern} = {Permission}",
            rule.EndpointPattern, rule.Permission);
        RefreshRules();
    }

    /// <summary>
    /// Approves a pending permission request.
    /// </summary>
    [RelayCommand]
    private void ApproveRequest(PermissionRequestViewModel request)
    {
        // Add as allowed and remove from pending
        _permissions.SetRule(
            ExtractBaseEndpoint(request.Endpoint),
            ToolPermission.AlwaysAllow,
            $"Approved: {request.Purpose}");

        PendingRequests.Remove(request);
        OnPropertyChanged(nameof(HasPendingRequests));
        RefreshRules();

        Logger.LogInformation(
            "Approved permission request for: {Endpoint}",
            request.Endpoint);
    }

    /// <summary>
    /// Denies a pending permission request.
    /// </summary>
    [RelayCommand]
    private void DenyRequest(PermissionRequestViewModel request)
    {
        // Add as denied and remove from pending
        _permissions.SetRule(
            ExtractBaseEndpoint(request.Endpoint),
            ToolPermission.Disabled,
            $"Denied: {request.Purpose}");

        PendingRequests.Remove(request);
        OnPropertyChanged(nameof(HasPendingRequests));
        RefreshRules();

        Logger.LogInformation(
            "Denied permission request for: {Endpoint}",
            request.Endpoint);
    }

    /// <summary>
    /// Clears all permission rules.
    /// </summary>
    [RelayCommand]
    private void ClearAllRules()
    {
        _permissions.ClearRules();
        Rules.Clear();
        Logger.LogInformation("Cleared all permission rules");
    }

    /// <summary>
    /// Adds common API patterns as pre-approved.
    /// </summary>
    [RelayCommand]
    private void AddCommonPatterns()
    {
        // These are commonly needed and relatively safe
        var commonPatterns = new[]
        {
            ("https://api.github.com", "GitHub API"),
            ("https://api.openweathermap.org", "Weather API"),
            ("https://api.exchangerate-api.com", "Exchange rates")
        };

        foreach (var (pattern, notes) in commonPatterns)
        {
            if (!_permissions.GetRules().ContainsKey(pattern))
            {
                _permissions.SetRule(pattern, ToolPermission.AlwaysAllow, notes);
            }
        }

        RefreshRules();
        Logger.LogInformation("Added common API patterns");
    }

    private void OnPermissionRequested(object? sender, InternetPermissionRequestEventArgs e)
    {
        // Add to pending requests (should dispatch to UI thread in real app)
        var request = new PermissionRequestViewModel(e.Endpoint, e.Purpose);

        // Avoid duplicates
        if (!PendingRequests.Any(r => r.Endpoint == e.Endpoint))
        {
            PendingRequests.Add(request);
            OnPropertyChanged(nameof(HasPendingRequests));
        }
    }

    private static string ExtractBaseEndpoint(string endpoint)
    {
        // Extract base URL for the rule (e.g., https://api.example.com)
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Host}";
        }
        return endpoint;
    }
}

/// <summary>
/// Display model for a permission rule.
/// </summary>
public sealed class PermissionRuleViewModel : ObservableObject
{
    public string EndpointPattern { get; }

    private ToolPermission _permission;
    public ToolPermission Permission
    {
        get => _permission;
        set => SetProperty(ref _permission, value);
    }

    public string? Notes { get; }

    public string PermissionIcon => Permission switch
    {
        ToolPermission.AlwaysAllow => "CheckCircle",
        ToolPermission.AlwaysAsk => "QuestionCircle",
        ToolPermission.Disabled => "XCircle",
        _ => "Circle"
    };

    public string PermissionColor => Permission switch
    {
        ToolPermission.AlwaysAllow => "Green",
        ToolPermission.AlwaysAsk => "Orange",
        ToolPermission.Disabled => "Red",
        _ => "Gray"
    };

    public PermissionRuleViewModel(string endpointPattern, ToolPermission permission, string? notes)
    {
        EndpointPattern = endpointPattern;
        _permission = permission;
        Notes = notes;
    }
}

/// <summary>
/// Display model for a pending permission request.
/// </summary>
public sealed record PermissionRequestViewModel(
    string Endpoint,
    string Purpose
)
{
    public DateTimeOffset RequestedAt { get; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Display model for permission options.
/// </summary>
public sealed record PermissionOption(
    ToolPermission Permission,
    string Name,
    string Description
);
