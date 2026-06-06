using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Model.DTO
{
    [Table("Transaction")]
    public class TransactionDTO : BaseDTO
    {
        [StringLength(250)]
        public required string Description { get; set; }

        public DateTime Date { get; set; }

        public decimal Amount { get; set; }

        public Repetition Repetition { get; set; }

        public int? TotalInstallments { get; set; }

        public Guid? InstallmentId { get; set; }

        public Guid? RecurringRuleId { get; set; }

        public int? Installment { get; set; }

        public int? CategoryId { get; set; }

        [NotMapped]
        public int? CategoryExternalId { get; set; }

        public CategoryDTO? Category { get; set; }

        public TransactionType Type { get; set; }

        public string? Note { get; set; }

        public int? AccountId { get; set; }

        [NotMapped]
        public int? AccountExternalId { get; set; }

        public AccountDTO? Account { get; set; }

        public required int UserId { get; set; }

        public UserDTO User { get; set; }

        public int? ExternalId { get; set; }

        /// <summary>
        /// True when this recurring occurrence has been individually edited by the user
        /// ("Apenas esta ocorrência"). Customized occurrences are physical transactions that
        /// must be synced with the server so that other devices do not overwrite them with
        /// a freshly generated projection.
        /// </summary>
        public bool IsCustomized { get; set; }
    }

    public enum TransactionType
    {
        Income,
        Expense,
        Transfer,
        Adjustment,
    }

    public enum Repetition
    {
        None    = 0,
        Monthly = 1,
        Recurring = 2,
    }
}
