using FsCheck.Xunit;
using Service.Transaction;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 7: Deterministic Recurring TransactionId
/// Validates: Requirements 6.1, 6.2
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "7")]
public class DeterministicGuidPropertyTests
{
    /// <summary>
    /// The same (RecurringRuleId, date) always produces the same Guid.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SameInputs_ProduceSameGuid(Guid ruleId, DateTime date)
    {
        var result1 = DeterministicGuid.FromRecurringRule(ruleId, date);
        var result2 = DeterministicGuid.FromRecurringRule(ruleId, date);

        return result1 == result2;
    }

    /// <summary>
    /// Distinct (RecurringRuleId, date) pairs produce distinct Guids.
    /// Generates two independent random pairs and skips when they happen to be equal.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DistinctInputs_ProduceDistinctGuids(
        Guid ruleId1, DateTime date1,
        Guid ruleId2, DateTime date2)
    {
        // Skip when both inputs happen to be equal (same pair)
        var sameInput = ruleId1 == ruleId2 && date1.Date == date2.Date;

        // Skip when either input is invalid (would return Guid.Empty)
        var invalidInput1 = ruleId1 == Guid.Empty || date1 == default;
        var invalidInput2 = ruleId2 == Guid.Empty || date2 == default;

        if (sameInput || invalidInput1 || invalidInput2)
            return true; // trivially true, skip

        var result1 = DeterministicGuid.FromRecurringRule(ruleId1, date1);
        var result2 = DeterministicGuid.FromRecurringRule(ruleId2, date2);

        return result1 != result2;
    }

    /// <summary>
    /// Guid.Empty ruleId returns Guid.Empty.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool EmptyRuleId_ReturnsEmptyGuid(DateTime date)
    {
        var result = DeterministicGuid.FromRecurringRule(Guid.Empty, date);

        return result == Guid.Empty;
    }

    /// <summary>
    /// default(DateTime) returns Guid.Empty.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DefaultDateTime_ReturnsEmptyGuid(Guid ruleId)
    {
        var result = DeterministicGuid.FromRecurringRule(ruleId, default);

        return result == Guid.Empty;
    }
}
