using System.Globalization;
using Model.DTO;

namespace XpemFinancial.Utils;

/// <summary>
/// Converts a CategoryType? to the corresponding color resource.
/// Income → green, Expense → red, null → transparent.
/// </summary>
public class CategoryTypeColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CategoryType type)
            return Colors.Transparent;

        var key = type switch
        {
            CategoryType.Income => "Income",
            CategoryType.Expense => "Expense",
            _ => null
        };

        if (key is not null && Application.Current?.Resources.TryGetValue(key, out var color) == true && color is Color c)
            return c;

        return Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
