using System.ComponentModel.DataAnnotations.Schema;

namespace Model.DTO
{
    [Table("VersionDbTables")]
    public class VersionDbTablesDTO
    {
        public int Id { get; set; }
        public int Version { get; set; }
    }
}
