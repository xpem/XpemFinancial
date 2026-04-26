using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace XpemFinancial.Utils
{
    public class AmountToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal amount)
            {
                // Verde para positivo, Vermelho para negativo
                return amount >= 0 ? Color.FromArgb("#3ca7f7") : Color.FromArgb("#f75c5c");
            }
            return Colors.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
