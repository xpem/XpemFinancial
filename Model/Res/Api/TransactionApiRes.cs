namespace Model.Resp.Api
{
    public class TransactionApiRes
    {
        /// <summary>
        /// Stable cross-device identifier for sync matching.
        /// Nullable because older servers might not return it.
        /// </summary>
        public Guid? TransactionId { get; set; }

        public int Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool Inactive { get; set; }

        public required string Description { get; set; }

        public DateTime Date { get; set; }

        public decimal Amount { get; set; }

        public int Repetition { get; set; }

        public int? TotalInstallments { get; set; }

        public Guid? InstallmentId { get; set; }

        public int? Installment { get; set; }

        public int? CategoryId { get; set; }

        public int Type { get; set; }

        public string? Note { get; set; }

        public int AccountId { get; set; }

        /// <summary>
        /// Stable identifier of the recurring rule that originated this occurrence.
        /// Populated by the server when the occurrence is a customized exception.
        /// </summary>
        public Guid? RecurringRuleId { get; set; }

        /// <summary>
        /// True when this occurrence has been individually edited and must survive
        /// scheduler regeneration on any device.
        /// </summary>
        public bool IsCustomized { get; set; }
    }
}
