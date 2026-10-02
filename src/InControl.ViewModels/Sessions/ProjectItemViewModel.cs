using System.ComponentModel;
using InControl.Core.Models;

namespace InControl.ViewModels.Sessions;

/// <summary>
/// One project row in the sidebar.
/// </summary>
public sealed class ProjectItemViewModel : INotifyPropertyChanged
{
    private string _name;
    private string? _instructions;
    private bool _isSelected;

    public ProjectItemViewModel(ChatProject project)
    {
        Id = project.Id;
        _name = project.Name;
        _instructions = project.Instructions;
    }

    /// <summary>
    /// Project id.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Name shown in the list.
    /// </summary>
    public string Name
    {
        get => _name;
        private set
        {
            if (_name == value)
                return;

            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    /// <summary>
    /// Standing instructions for every session in the project.
    /// </summary>
    public string? Instructions => _instructions;

    /// <summary>
    /// Whether this project is the one the session list is showing.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    /// <summary>
    /// Copies a saved project into this row.
    /// </summary>
    public void Update(ChatProject project)
    {
        Name = project.Name;
        if (_instructions == project.Instructions)
            return;

        _instructions = project.Instructions;
        OnPropertyChanged(nameof(Instructions));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
