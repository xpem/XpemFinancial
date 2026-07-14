using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.DTO
{
    [Table("Category")]
    public class CategoryDTO : BaseDTO
    {
        /// <summary>
        /// Stable cross-device identifier assigned at creation time.
        /// Used as the primary key for sync matching.
        /// Default: Guid.Empty for legacy records (backward-compatible).
        /// </summary>
        public Guid CategoryId { get; set; }

        public int? ExternalId { get; set; }

        [StringLength(50)]
        public string Name { get; set; }

        public int? ParentExternalId { get; set; }

        public bool IsMainCategory { get; set; }

        public int UserId { get; set; }

        public UserDTO User { get; set; }

        public bool SystemDefault { get; set; }
    }
}
