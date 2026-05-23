using System.ComponentModel.DataAnnotations.Schema;

namespace Model.DTO
{
    [Table("Category")]
    public class CategoryDTO : BaseDTO
    {
        public int? ExternalId { get; set; }

        public string Name { get; set; }

        public int? ParentExternalId { get; set; }

        public bool IsMainCategory { get; set; }

        public int UserId { get; set; }

        public UserDTO User { get; set; }

        public bool SystemDefault { get; set; }
    }
}
