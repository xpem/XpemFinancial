using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Service.Transaction;

/// <summary>
/// Provides deterministic Guid generation for recurring transaction occurrences.
/// Given the same RecurringRuleId and occurrence date, produces a byte-identical Guid
/// across any device or execution context.
/// </summary>
public static class DeterministicGuid
{
    // Fixed namespace UUID (generated once, never changed)
    private static readonly Guid Namespace = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    /// <summary>
    /// Derives a deterministic Guid from a RecurringRuleId and an occurrence date.
    /// Uses SHA-256 truncated to 16 bytes with version/variant bits set per RFC 4122 (v5-style).
    /// </summary>
    /// <param name="recurringRuleId">The recurring rule identifier. Must not be Guid.Empty.</param>
    /// <param name="occurrenceDate">The occurrence date. Must not be default(DateTime). Only the date component is used.</param>
    /// <returns>A deterministic Guid, or Guid.Empty if inputs are invalid.</returns>
    public static Guid FromRecurringRule(Guid recurringRuleId, DateTime occurrenceDate)
    {
        if (recurringRuleId == Guid.Empty || occurrenceDate == default)
            return Guid.Empty;

        var dateOnly = occurrenceDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Build input: namespace bytes + ruleId bytes + date-only string bytes (UTF-8)
        var namespaceBytes = Namespace.ToByteArray();
        var ruleIdBytes = recurringRuleId.ToByteArray();
        var dateBytes = Encoding.UTF8.GetBytes(dateOnly);

        var inputLength = namespaceBytes.Length + ruleIdBytes.Length + dateBytes.Length;
        var input = new byte[inputLength];

        Buffer.BlockCopy(namespaceBytes, 0, input, 0, namespaceBytes.Length);
        Buffer.BlockCopy(ruleIdBytes, 0, input, namespaceBytes.Length, ruleIdBytes.Length);
        Buffer.BlockCopy(dateBytes, 0, input, namespaceBytes.Length + ruleIdBytes.Length, dateBytes.Length);

        var hash = SHA256.HashData(input);

        // Take first 16 bytes
        var guidBytes = hash[..16];

        // Set UUID v5 version bits: byte[6] high nibble = 0101 (version 5)
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);

        // Set variant bits: byte[8] high bits = 10 (RFC 4122)
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes);
    }
}
