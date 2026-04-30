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
        public string Description { get; set; }

        public DateTime Date { get; set; }

        public decimal Amount { get; set; }

        public Repetition Repetition { get; set; }

        public int Installments { get; set; }

        public int CategoryId { get; set; }

        public CategoryDTO? Category { get; set; }

        public TransactionType Type { get; set; }
    }
}
