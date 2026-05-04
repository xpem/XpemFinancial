using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace XpemFinancial.Utils
{
    public class BoldConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? FontAttributes.Bold : FontAttributes.None;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
