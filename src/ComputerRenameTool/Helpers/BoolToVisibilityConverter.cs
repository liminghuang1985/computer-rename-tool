using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ComputerRenameTool.Helpers;

/// <summary>
/// Maps <c>true</c> → <see cref="Visibility.Visible"/>, <c>false</c> →
/// <see cref="Visibility.Collapsed"/>. Hand-rolled to avoid a value-converter
/// dependency.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}
