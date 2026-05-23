using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Model.Resp.Api
{
    public record TransactionCategoryApiRes
    {
        public int Id { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool SystemDefault { get; set; }

        public required string Name { get; set; }

        public bool Inactive { get; set; }

        public string? Color { get; set; }

        public bool IsMainTransactionCategory { get; set; }

        public int? ParentTransactionCategoryId { get; set; }
    }
}
