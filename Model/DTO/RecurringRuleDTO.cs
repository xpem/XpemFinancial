using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.DTO
{
    [Table("RecurringRule")]
    public class RecurringRuleDTO : BaseDTO
    {
        public Guid RecurringRuleId { get; set; }

        [StringLength(100)]
        public string? Description { get; set; }

        public decimal Amount { get; set; }

        public TransactionType Type { get; set; }

        public int CategoryId { get; set; }

        [NotMapped]
        public int? CategoryExternalId { get; set; }

        public int? AccountId { get; set; }

        public Frequency Frequency { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public required int UserId { get; set; }
    }

    public enum Frequency
    {
        //Daily   = 0,
        //Weekly  = 1,
        Monthly = 2,
        //Yearly  = 3,
    }
}
