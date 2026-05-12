using System.ComponentModel.DataAnnotations.Schema;

namespace Model.DTO
{
    [Table("Category")]
    public class CategoryDTO : BaseDTO
    {
        public string Name { get; set; }

        public int? ParentId { get; set; }

        public bool IsMainCategory { get; set; }

        public int UserId { get; set; }

        public UserDTO User { get; set; }
    }
}
