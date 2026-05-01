using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Model.DTO
{
    [Table("User")]
    public class UserDTO : BaseDTO
    {
        public string? Name { get; set; }

        public string? Email { get; set; }

        public decimal Balance { get; set; }

        public string? Token { get; set; }

        public DateTime LastUpdate { get; set; }

        //por enquanto, um usuário só tem uma conta, mas futuramente pode ter mais de uma
        public AccountDTO? Account { get; set; }

    }
}
