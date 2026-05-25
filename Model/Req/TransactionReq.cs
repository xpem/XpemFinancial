using Model.DTO;
using System.ComponentModel.DataAnnotations;

namespace Model.Req
{
    public class TransactionReq
    {
        public int? Id { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool Inactive { get; set; }

        public required string Description { get; set; }

        public DateTime Date { get; set; }

        public decimal Amount { get; set; }

        public Repetition Repetition { get; set; }

        public int? TotalInstallments { get; set; }

        public Guid? InstallmentId { get; set; }

        public int? Installment { get; set; }

        public int? CategoryId { get; set; }

        public TransactionType Type { get; set; }

        public string? Note { get; set; }

        public int AccountId { get; set; }
    }
}
