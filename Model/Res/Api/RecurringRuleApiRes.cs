namespace Model.Resp.Api
{
    public class RecurringRuleApiRes
    {
        public int Id { get; set; }

        public Guid RecurringRuleId { get; set; }

        public string? Description { get; set; }

        public decimal Amount { get; set; }

        public int Type { get; set; }

        public int? CategoryId { get; set; }

        public int? AccountId { get; set; }

        public int Frequency { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public bool Inactive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
