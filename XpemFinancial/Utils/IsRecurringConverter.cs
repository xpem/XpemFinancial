using System.Globalization;

namespace XpemFinancial.Utils;

/// <summary>
/// Returns true (visible) when the bound value is a non-null Guid,
/// i.e. when a TransactionDTO.RecurringRuleId is set.
/// </summary>
public class IsRecurringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Guid id && id != Guid.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
