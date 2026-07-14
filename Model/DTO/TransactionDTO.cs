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
        /// <summary>
        /// Stable cross-device identifier assigned at creation time.
        /// Used as the primary key for sync matching.
        /// Default: Guid.Empty for legacy records (backward-compatible).
        /// </summary>
        public Guid TransactionId { get; set; }

        [StringLength(100)]
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

        [StringLength(500)]
        public string? Note { get; set; }

        public int? AccountId { get; set; }

        [NotMapped]
        public int? AccountExternalId { get; set; }

        public AccountDTO? Account { get; set; }

        public int? DestinationAccountId { get; set; }

        [NotMapped]
        public int? DestinationAccountExternalId { get; set; }

        [ForeignKey("DestinationAccountId")]
        public AccountDTO? DestinationAccount { get; set; }

        public required int UserId { get; set; }

        public UserDTO User { get; set; }

        public int? ExternalId { get; set; }

        /// <summary>
        /// Controls the synchronization lifecycle of this transaction.
        /// Used to prevent race conditions between concurrent push and pull operations.
        /// </summary>
        public TransactionSyncStatus SyncStatus { get; set; }

        /// <summary>
        /// True when this recurring occurrence has been individually edited by the user
        /// ("Apenas esta ocorrência"). Customized occurrences are physical transactions that
        /// must be synced with the server so that other devices do not overwrite them with
        /// a freshly generated projection.
        /// </summary>
        public bool IsCustomized { get; set; }
    }

    /// <summary>
    /// Represents the synchronization state of a transaction.
    /// </summary>
    public enum TransactionSyncStatus
    {
        /// <summary>Already synced or does not need push (e.g. pulled from server).</summary>
        Synced = 0,

        /// <summary>Needs to be pushed to the server (created/updated locally).</summary>
        Pending = 1,

        /// <summary>Push is currently in-flight — pull must not overwrite.</summary>
        Pushing = 2,
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
