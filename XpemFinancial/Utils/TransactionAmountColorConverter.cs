using System.Globalization;
using Model.DTO;

namespace XpemFinancial.Utils;

/// <summary>
/// Returns the appropriate color for a transaction's amount display based on its Type:
/// - Transfer → neutral blue (#6cc8ef)
/// - Income (positive amount) → green (#2bbf69)
/// - Expense (negative amount) → red (#f75c5c)
///
/// Used as IMultiValueConverter with bindings to Amount and Type.
/// </summary>
public class TransactionAmountColorConverter : IMultiValueConverter
{
    private static readonly Color TransferColor = Color.FromArgb("#6cc8ef");
    private static readonly Color IncomeColor = Color.FromArgb("#2bbf69");
    private static readonly Color ExpenseColor = Color.FromArgb("#f75c5c");

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return Colors.White;

        var type = values[1] is TransactionType t ? t : TransactionType.Expense;

        if (type == TransactionType.Transfer)
            return TransferColor;

        var amount = values[0] is decimal a ? a : 0m;
        return amount >= 0 ? IncomeColor : ExpenseColor;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
