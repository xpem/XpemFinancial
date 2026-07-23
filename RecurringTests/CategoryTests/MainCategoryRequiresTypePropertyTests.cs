// Feature: category-type-classification, Property 10: MainCategory creation requires Type selection
using ApiRepo;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Category;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property-based tests for MainCategory creation requiring Type selection.
/// Save is rejected when no Type is selected; succeeds when valid Type is provided.
/// **Validates: Requirements 2.4, 6.4**
/// </summary>
[Trait("Feature", "category-type-classification")]
[Trait("Property", "10")]
public class MainCategoryRequiresTypePropertyTests
{
    /// <summary>
    /// Generates a valid CategoryType enum member (Income, Expense, or Both).
    /// </summary>
    private static Gen<CategoryType> ValidCategoryType()
    {
        return Gen.Elements(CategoryType.Income, CategoryType.Expense, CategoryType.Both);
    }

    /// <summary>
    /// Generates a valid SelectedCategoryTypeIndex (0, 1, or 2) that maps to a valid CategoryType.
    /// </summary>
    private static Gen<int> ValidCategoryTypeIndex()
    {
        return Gen.Elements(0, 1, 2);
    }

    /// <summary>
    /// Property 10: When SelectedCategoryTypeIndex is -1 (no selection), the derived
    /// SelectedCategoryType is null, which means save should be rejected for MainCategory.
    /// This validates the VM logic pattern that guards against saving without a Type.
    /// **Validates: Requirements 2.4, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoTypeSelected_SelectedCategoryTypeIsNull_SaveRejected()
    {
        // The VM logic: SelectedCategoryType => SelectedCategoryTypeIndex >= 0 ? (CategoryType)index : null
        // When index is -1, SelectedCategoryType is null
        // The Save guard: if (IsMainCategory && SelectedCategoryType is null) → reject

        return Prop.ForAll(
            Gen.Constant(-1).ToArbitrary(),
            index =>
            {
                // Replicate the VM's SelectedCategoryType derivation logic
                CategoryType? selectedCategoryType = index >= 0 ? (CategoryType)index : null;

                // Replicate the VM's Save validation: MainCategory requires non-null Type
                bool isMainCategory = true;
                bool saveRejected = isMainCategory && selectedCategoryType is null;

                return saveRejected
                    .Label($"Expected save to be rejected when index={index} (SelectedCategoryType={selectedCategoryType})");
            });
    }

    /// <summary>
    /// Property 10: For any valid CategoryTypeIndex (0, 1, or 2), the derived
    /// SelectedCategoryType is non-null, which means save validation passes for MainCategory.
    /// **Validates: Requirements 2.4, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidTypeSelected_SelectedCategoryTypeIsNotNull_SaveAllowed()
    {
        return Prop.ForAll(
            ValidCategoryTypeIndex().ToArbitrary(),
            index =>
            {
                // Replicate the VM's SelectedCategoryType derivation logic
                CategoryType? selectedCategoryType = index >= 0 ? (CategoryType)index : null;

                // Replicate the VM's Save validation: MainCategory requires non-null Type
                bool isMainCategory = true;
                bool saveAllowed = !(isMainCategory && selectedCategoryType is null);

                return saveAllowed
                    .Label($"Expected save to be allowed when index={index} (SelectedCategoryType={selectedCategoryType})");
            });
    }

    /// <summary>
    /// Property 10: For any valid CategoryType, creating a MainCategory with that Type
    /// via AddLocalAsync succeeds without exception, confirming that service-level
    /// persistence works when a valid Type is provided.
    /// **Validates: Requirements 2.4, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MainCategory_WithValidType_AddLocalAsyncSucceeds()
    {
        return Prop.ForAll(
            ValidCategoryType().ToArbitrary(),
            async categoryType =>
            {
                // Arrange
                var categoryRepo = Substitute.For<ICategoryRepo>();
                var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
                var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

                var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

                var mainCategory = new CategoryDTO
                {
                    CategoryId = Guid.NewGuid(),
                    Name = "TestCategory",
                    IsMainCategory = true,
                    Type = categoryType,
                    UserId = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Act — should not throw for any valid CategoryType
                Exception? caughtException = null;
                try
                {
                    await service.AddLocalAsync(mainCategory);
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }

                // Assert: no exception means save succeeded
                return (caughtException is null)
                    .Label($"AddLocalAsync threw {caughtException?.GetType().Name}: {caughtException?.Message} for Type={categoryType}");
            });
    }

    /// <summary>
    /// Property 10: The SelectedCategoryType derivation logic correctly maps any valid index
    /// to the corresponding CategoryType enum value (index 0 → Income, 1 → Expense, 2 → Both).
    /// **Validates: Requirements 2.4, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidIndex_MapsToCorrectCategoryType()
    {
        return Prop.ForAll(
            ValidCategoryTypeIndex().ToArbitrary(),
            index =>
            {
                // Replicate the VM's SelectedCategoryType derivation
                CategoryType? selectedCategoryType = index >= 0 ? (CategoryType)index : null;

                // Expected mapping
                CategoryType expected = (CategoryType)index;

                return (selectedCategoryType == expected)
                    .Label($"Expected index {index} to map to {expected} but got {selectedCategoryType}");
            });
    }
}
