using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XpemFinancial.Utils;

namespace XpemFinancial.Utils
{
    public class IndentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? new Thickness(0) : new Thickness(20, 0, 0, 0);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
