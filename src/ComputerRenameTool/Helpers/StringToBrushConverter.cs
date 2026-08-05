using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ComputerRenameTool.Helpers;

/// <summary>
/// Converts a hex color string (e.g. <c>#1B8E3B</c>) into a WPF
/// <see cref="SolidColorBrush"/>. Lets view-models expose plain color
/// constants without having to know about WPF's brush type.
/// </summary>
public sealed class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && TryParse(s, out var color))
        {
            return new SolidColorBrush(color);
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static bool TryParse(string hex, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
            return true;
        }
        catch
        {
            color = Colors.Black;
            return false;
        }
    }
}
