using Microsoft.Maui.Controls;
using System.Globalization;

namespace XpemFinancial.Converters;

public class IndentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? new Thickness(15, 0, 0, 0) : new Thickness(30, 0, 0, 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}