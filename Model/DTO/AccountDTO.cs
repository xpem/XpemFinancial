using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Model.DTO
{
    [Table("Account")]
    public class AccountDTO : BaseDTO
    {
        [StringLength(100)]
        public required string Name { get; set; }

        public AccountType Type { get; set; }

        public decimal CurrentBalance { get; set; }

        public bool IncludeInGeneralBalance { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public required int UserId { get; set; }
    }
}
