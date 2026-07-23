// Feature: category-type-classification, Property 1: CategoryType enum serialization round-trip
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property-based tests for CategoryType enum serialization round-trip.
/// For any valid CategoryType, casting to int and back produces the original member.
/// **Validates: Requirements 1.3**
/// </summary>
[Trait("Feature", "category-type-classification")]
[Trait("Property", "1")]
public class CategoryTypeSerializationPropertyTests
{
    /// <summary>
    /// Generates a valid CategoryType enum member (Income, Expense, or Both).
    /// </summary>
    private static Gen<CategoryType> ValidCategoryType()
    {
        return Gen.Elements(CategoryType.Income, CategoryType.Expense, CategoryType.Both);
    }

    /// <summary>
    /// Property 1: For any valid CategoryType enum member, casting to int and back
    /// to CategoryType produces the original enum member.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CastToInt_AndBack_ProducesOriginalMember()
    {
        return Prop.ForAll(
            ValidCategoryType().ToArbitrary(),
            original =>
            {
                // Act - serialize to int and deserialize back
                int asInt = (int)original;
                CategoryType roundTripped = (CategoryType)asInt;

                // Assert - round-trip preserves the original value
                return (roundTripped == original)
                    .Label($"Expected {original} but got {roundTripped} (int value: {asInt})");
            });
    }

    /// <summary>
    /// Property 1: The integer representations match the spec-defined values
    /// (Income = 0, Expense = 1, Both = 2), ensuring client-server serialization compatibility.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IntegerValues_MatchSpecDefinedMapping()
    {
        return Prop.ForAll(
            ValidCategoryType().ToArbitrary(),
            categoryType =>
            {
                int intValue = (int)categoryType;

                bool correctMapping = categoryType switch
                {
                    CategoryType.Income => intValue == 0,
                    CategoryType.Expense => intValue == 1,
                    CategoryType.Both => intValue == 2,
                    _ => false
                };

                return correctMapping
                    .Label($"{categoryType} should map to expected int, got {intValue}");
            });
    }
}
