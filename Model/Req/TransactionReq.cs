using Model.DTO;
using System.ComponentModel.DataAnnotations;

namespace Model.Req
{
    public class TransactionReq
    {
        public int? Id { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool Inactive { get; set; }

        [StringLength(100)]
        public required string Description { get; set; }

        public DateTime Date { get; set; }

        public decimal Amount { get; set; }

        public Repetition Repetition { get; set; }

        public int? TotalInstallments { get; set; }

        public Guid? InstallmentId { get; set; }

        public int? Installment { get; set; }

        public int? CategoryId { get; set; }

        public TransactionType Type { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public int AccountId { get; set; }

        public int? DestinationAccountId { get; set; }

        /// <summary>
        /// Stable cross-device identifier assigned at creation time.
        /// Used by the server for upsert-based deduplication instead of heuristic matching.
        /// Nullable for backward compatibility with older clients.
        /// </summary>
        public Guid? TransactionId { get; set; }

        /// <summary>
        /// Stable identifier of the recurring rule that originated this occurrence.
        /// Sent to the server so it can associate the customized occurrence with its rule.
        /// </summary>
        public Guid? RecurringRuleId { get; set; }

        /// <summary>
        /// True when this occurrence has been individually edited and must not be
        /// overwritten by scheduler projections on any device.
        /// </summary>
        public bool IsCustomized { get; set; }
    }
}
