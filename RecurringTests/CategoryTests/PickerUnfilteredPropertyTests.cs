#if WINDOWS
// Feature: category-type-classification, Property 6: CategoryPicker shows all active categories for Transfer, Adjustment, or null context
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;
using XpemFinancial.VMs;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property 6: CategoryPicker shows all active categories for Transfer, Adjustment, or null context
/// For any list of active categories, calling FilterByTransactionType with Transfer, Adjustment, or null
/// returns all categories regardless of their CategoryType.
/// **Validates: Requirements 7.3, 7.4, 11.4**
/// </summary>
[Trait("Feature", "category-type-classification")]
[Trait("Property", "6")]
public class PickerUnfilteredPropertyTests
{
    /// <summary>
    /// Generates an arbitrary CategoryDTO with a random CategoryType.
    /// </summary>
    private static Gen<CategoryDTO> CategoryGen()
    {
        return from name in Gen.Elements("Food", "Transport", "Health", "Salary", "Rent", "Entertainment", "Gifts", "Insurance")
               from isMain in Gen.Elements(true, false)
               from type in Gen.Elements(CategoryType.Income, CategoryType.Expense, CategoryType.Both)
               from extId in Gen.Choose(1, 1000)
               from parentExtId in Gen.Choose(1, 100)
               select new CategoryDTO
               {
                   CategoryId = Guid.NewGuid(),
                   ExternalId = extId,
                   Name = name,
                   IsMainCategory = isMain,
                   ParentExternalId = isMain ? null : parentExtId,
                   Inactive = false,
                   UserId = 1,
                   SystemDefault = false,
                   Type = type,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generates an arbitrary list of active categories with varying CategoryType values.
    /// </summary>
    private static Gen<List<CategoryDTO>> CategoryListGen()
    {
        return from count in Gen.Choose(0, 30)
               from categories in Gen.ListOf(CategoryGen(), count)
               select categories.ToList();
    }

    /// <summary>
    /// For any list of active categories, FilterByTransactionType with TransactionType.Transfer
    /// returns all categories — no filtering is applied.
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Transfer_ReturnsAllCategories()
    {
        return Prop.ForAll(CategoryListGen().ToArbitrary(), (List<CategoryDTO> categories) =>
        {
            var result = CategoryPickerVM.FilterByTransactionType(categories, TransactionType.Transfer);

            return (result.Count == categories.Count)
                .Label($"Transfer context: expected {categories.Count} categories, got {result.Count}");
        });
    }

    /// <summary>
    /// For any list of active categories, FilterByTransactionType with TransactionType.Adjustment
    /// returns all categories — no filtering is applied.
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Adjustment_ReturnsAllCategories()
    {
        return Prop.ForAll(CategoryListGen().ToArbitrary(), (List<CategoryDTO> categories) =>
        {
            var result = CategoryPickerVM.FilterByTransactionType(categories, TransactionType.Adjustment);

            return (result.Count == categories.Count)
                .Label($"Adjustment context: expected {categories.Count} categories, got {result.Count}");
        });
    }

    /// <summary>
    /// For any list of active categories, FilterByTransactionType with null transaction type
    /// returns all categories — no filtering is applied.
    /// **Validates: Requirements 7.4, 11.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullContext_ReturnsAllCategories()
    {
        return Prop.ForAll(CategoryListGen().ToArbitrary(), (List<CategoryDTO> categories) =>
        {
            var result = CategoryPickerVM.FilterByTransactionType(categories, null);

            return (result.Count == categories.Count)
                .Label($"Null context: expected {categories.Count} categories, got {result.Count}");
        });
    }

    /// <summary>
    /// For any list of active categories and any of the three unfiltered contexts (Transfer, Adjustment, null),
    /// the returned list contains exactly the same category instances as the input.
    /// **Validates: Requirements 7.3, 7.4, 11.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnfilteredContexts_ReturnSameInstances()
    {
        var contextGen = Gen.Elements<TransactionType?>(TransactionType.Transfer, TransactionType.Adjustment, null);

        var scenarioGen =
            from categories in CategoryListGen()
            from context in contextGen
            select (Categories: categories, Context: context);

        return Prop.ForAll(scenarioGen.ToArbitrary(), scenario =>
        {
            var result = CategoryPickerVM.FilterByTransactionType(scenario.Categories, scenario.Context);

            // For Transfer/Adjustment/null, the method returns the same list reference
            bool sameReference = ReferenceEquals(result, scenario.Categories);

            return sameReference
                .Label($"Context '{scenario.Context}': expected same list reference (no filtering)");
        });
    }
}
#endif
