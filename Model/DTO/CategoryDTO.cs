using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Model.DTO
{
    [Table("Category")]
    public class CategoryDTO : BaseDTO
    {
        public string Name { get; set; }

        public int? ParentId { get; set; }

        public bool IsMainCategory { get; set; }
    }
}
