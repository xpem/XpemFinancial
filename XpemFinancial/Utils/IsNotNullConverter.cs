using System.Globalization;

namespace XpemFinancial.Utils;

/// <summary>
/// Returns true when the bound value is not null.
/// Generic converter useful for visibility bindings.
/// </summary>
public class IsNotNullConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
