using System.Globalization;
using Microsoft.Maui.Controls;

namespace XpemFinancial.Utils;

/// <summary>
/// Returns <see cref="FontAttributes.Italic"/> when the bound DateTime is strictly
/// in the future (after today), otherwise <see cref="FontAttributes.None"/>.
/// </summary>
public class IsFutureDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateTime date && date.Date > DateTime.Today
            ? FontAttributes.Italic
            : FontAttributes.None;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
