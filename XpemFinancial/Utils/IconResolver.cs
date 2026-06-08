using System.Globalization;
using System.Reflection;
using XpemFinancial.Resources;

namespace XpemFinancial.Utils
{
    public class IconResolver : IValueConverter
    {
        private static readonly Dictionary<string, string> _icons = BuildIconMap();

        private static Dictionary<string, string> BuildIconMap()
        {
            return typeof(IconFont)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name && _icons.TryGetValue(name, out var glyph))
                return glyph;

            return IconFont.Tag; // fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
