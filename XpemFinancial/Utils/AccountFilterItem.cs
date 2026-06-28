namespace XpemFinancial.Utils
{
    /// <summary>
    /// Represents an item in the Dashboard account filter picker.
    /// AccountId = null means "Todas as Contas (Consolidado)".
    /// </summary>
    public class AccountFilterItem
    {
        public int? AccountId { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public override string ToString() => DisplayName;

        public override bool Equals(object? obj)
        {
            if (obj is AccountFilterItem other)
                return AccountId == other.AccountId;
            return false;
        }

        public override int GetHashCode() => AccountId.GetHashCode();
    }
}
