namespace Model
{
    public class Transaction
    {
        public string Description { get; set; }
        public DateTime Date { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
    }
}
