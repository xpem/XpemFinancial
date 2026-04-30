using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Model.DTO
{
    public class BaseDTO
    {
        [Key]
        public int Id { get; set; }

        public int? ExternalId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool Inactive { get; set; }
    }
}
