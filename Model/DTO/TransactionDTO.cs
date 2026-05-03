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

        public int? Installment { get; set; }

        public int CategoryId { get; set; }

        public CategoryDTO? Category { get; set; }

        public TransactionType Type { get; set; }

        public string? Note { get; set; }

        public bool IsDeleted { get; set; }

        public int AccountId { get; set; }

        public AccountDTO? Account { get; set; }

        public int UserId { get; set; }

        public UserDTO User { get; set; }
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
