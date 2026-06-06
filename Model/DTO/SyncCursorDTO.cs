using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.DTO
{
    /// <summary>
    /// Stores the last server-side timestamp used as a delta-pull anchor for each entity type.
    /// Using the server's own UpdatedAt values (received in pull responses) avoids clock-skew
    /// problems that arise when the device clock is ahead or behind the server.
    /// </summary>
    [Table("SyncCursor")]
    public class SyncCursorDTO
    {
        /// <summary>
        /// Stable entity name used as a primary key (e.g. "Transaction", "RecurringRule").
        /// </summary>
        [Key]
        [StringLength(50)]
        public required string EntityName { get; set; }

        /// <summary>
        /// The highest UpdatedAt value seen in the last successful pull response from the server.
        /// Sent as the <c>updatedAt</c> query parameter on the next delta pull.
        /// </summary>
        public DateTime ServerTimestamp { get; set; }
    }
}
