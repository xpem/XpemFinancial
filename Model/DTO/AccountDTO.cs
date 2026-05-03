using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Model.DTO
{
    [Table("Account")]
    public class AccountDTO : BaseDTO
    {

        //public string Name { get; set; }

        public decimal Balance { get; set; }

        public required int UserId { get; set; }

        public UserDTO? User { get; set; }
    }
}
