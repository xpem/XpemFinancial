using System.Globalization;

namespace XpemFinancial.Utils
{
    /// <summary>
    /// Represents a selectable month in the navigation picker.
    /// </summary>
    public class MonthOption
    {
        public DateTime Date { get; }
        public string DisplayName { get; }

        public MonthOption(DateTime date)
        {
            Date = date;
            DisplayName = date.ToString("MMMM/yyyy", CultureInfo.CurrentCulture);
        }

        public override string ToString() => DisplayName;

        public override bool Equals(object? obj)
        {
            if (obj is MonthOption other)
                return Date.Year == other.Date.Year && Date.Month == other.Date.Month;
            return false;
        }

        public override int GetHashCode() => HashCode.Combine(Date.Year, Date.Month);
    }
}
