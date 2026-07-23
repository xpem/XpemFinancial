using System.Globalization;
using Model.DTO;
using XpemFinancial.Resources;

namespace XpemFinancial.Utils;

/// <summary>
/// Converts a CategoryType? to an icon font character.
/// Income → ArrowTrendUp, Expense → ArrowTrendDown, null → empty.
/// </summary>
public class CategoryTypeIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is CategoryType type
            ? type switch
            {
                CategoryType.Income => IconFont.ArrowTrendUp,
                CategoryType.Expense => IconFont.ArrowTrendDown,
                _ => string.Empty
            }
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
