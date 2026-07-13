using System.ComponentModel.DataAnnotations;

namespace Model.Req
{
    public record CategoryReq
    {
        public Guid? CategoryId { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        public required string Name { get; set; }

        public bool IsMainTransactionCategory { get; set; }

        public int? ParentTransactionCategoryId { get; set; }

        public bool Inactive { get; set; }

        [StringLength(8)]
        public string? Color { get; set; }
    }
}
