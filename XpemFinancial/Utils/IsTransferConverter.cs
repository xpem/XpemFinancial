using System.Globalization;
using Model.DTO;

namespace XpemFinancial.Utils;

/// <summary>
/// Returns true when the bound TransactionType value equals Transfer.
/// Used to control visibility of transfer-specific UI elements.
/// </summary>
public class IsTransferConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TransactionType type && type == TransactionType.Transfer;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
