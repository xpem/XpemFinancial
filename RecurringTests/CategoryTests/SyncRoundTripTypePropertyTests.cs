// Feature: category-type-classification, Property 7: Push/Pull sync round-trip preserves Type
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Service.Category;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property-based tests for Push/Pull sync round-trip preserving CategoryType.
/// For any valid CategoryType, push serialization (cast to int) followed by
/// pull deserialization (SafeParseCategoryType) produces the original value.
/// **Validates: Requirements 8.1, 8.2, 9.1, 9.2**
/// </summary>
[Trait("Feature", "category-type-classification")]
[Trait("Property", "7")]
public class SyncRoundTripTypePropertyTests
{
    /// <summary>
    /// Generates a valid CategoryType enum member (Income, Expense, or Both).
    /// </summary>
    private static Gen<CategoryType> ValidCategoryType()
    {
        return Gen.Elements(CategoryType.Income, CategoryType.Expense, CategoryType.Both);
    }

    /// <summary>
    /// Property 7: For any valid CategoryType, pushing (serializing as int) then
    /// pulling (deserializing via SafeParseCategoryType) yields the same Type value.
    /// This simulates the full sync round-trip: Push sends (int)category.Type in CategoryReq,
    /// Pull receives that int and maps it back via SafeParseCategoryType.
    /// **Validates: Requirements 8.1, 8.2, 9.1, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PushThenPull_PreservesCategoryType()
    {
        return Prop.ForAll(
            ValidCategoryType().ToArbitrary(),
            original =>
            {
                // Simulate Push: serialize Type as int (what CategoryReq sends)
                int pushed = (int)original;

                // Simulate Pull: deserialize int via SafeParseCategoryType (what PullAsync does)
                CategoryType pulled = CategoryService.SafeParseCategoryType(pushed);

                // Assert: round-trip preserves the original value
                return (pulled == original)
                    .Label($"Expected {original} but got {pulled} after push({pushed})/pull round-trip");
            });
    }
}
