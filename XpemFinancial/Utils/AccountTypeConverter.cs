using Model.DTO;
using System.Globalization;

namespace XpemFinancial.Utils
{
    public class AccountTypeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is AccountType type)
            {
                return type switch
                {
                    AccountType.Checking => "Corrente",
                    AccountType.Savings => "Investimentos",
                    AccountType.Benefits => "Benefícios",
                    _ => type.ToString()
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
