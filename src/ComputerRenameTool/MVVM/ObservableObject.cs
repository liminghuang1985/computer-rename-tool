using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ComputerRenameTool.MVVM;

/// <summary>
/// Minimal hand-rolled replacement for CommunityToolkit.Mvvm.ObservableObject.
/// Avoids the third-party dependency required by DESIGN.md §11.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets the field, raises <see cref="PropertyChanged"/> when the value changed.
    /// </summary>
    /// <returns><c>true</c> when the value actually changed.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for the given property. Pass <see cref="string.Empty"/>
    /// to notify "all properties" — required for some WPF bindings that read computed state.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
