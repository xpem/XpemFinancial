namespace Model
{
    public class Transaction
    {
        public int Id { get; set; }

        public string Description { get; set; }

        public DateTime Date { get; set; }

        public decimal Amount { get; set; }

        public Repetition Repetition { get; set; }

        public int Installments { get; set; }

        public int CategoryId { get; set; }

        public Category? Category { get; set; }

        public TransactionType Type { get; set; }
    }

    public enum TransactionType
    {
        Income,
        Expense,
        Transfer
    }

    public enum Repetition
    {
        None,
        Monthly,
        //Advanced
    }
}
