// Feature: category-type-classification, Property 5: CategoryPicker filters by compatible Type for Income and Expense contexts
#if WINDOWS
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;
using XpemFinancial.VMs;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property-based tests for CategoryPicker filtering by transaction type context.
/// For any list of active categories, Income context returns only Income + Both;
/// Expense context returns only Expense + Both.
/// **Validates: Requirements 7.1, 7.2**
/// </summary>
[Trait("Feature", "category-type-classification")]
[Trait("Property", "5")]
public class PickerFilterByTypePropertyTests
{
    /// <summary>
    /// Generates an arbitrary CategoryDTO with a random CategoryType.
    /// </summary>
    private static Gen<CategoryDTO> CategoryWithRandomTypeGen()
    {
        return from name in Gen.Elements("Salary", "Rent", "Food", "Transport", "Gifts", "Health", "Education", "Other")
               from type in Gen.Elements(CategoryType.Income, CategoryType.Expense, CategoryType.Both)
               from extId in Gen.Choose(1, 10000)
               select new CategoryDTO
               {
                   CategoryId = Guid.NewGuid(),
                   ExternalId = extId,
                   Name = name,
                   IsMainCategory = true,
                   ParentExternalId = null,
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
               from categories in Gen.ListOf(CategoryWithRandomTypeGen(), count)
               select categories.ToList();
    }

    /// <summary>
    /// Property 5a: For Income context, ALL returned categories have Type == Income OR Type == Both.
    /// NO categories with Type == Expense appear in the filtered result.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IncomeContext_ReturnsOnly_IncomeAndBoth()
    {
        return Prop.ForAll(CategoryListGen().ToArbitrary(), (List<CategoryDTO> categories) =>
        {
            // Act
            var filtered = CategoryPickerVM.FilterByTransactionType(categories, TransactionType.Income);

            // Assert: every returned category must be Income or Both
            bool allCompatible = filtered.All(c => c.Type == CategoryType.Income || c.Type == CategoryType.Both);

            // Assert: no Expense-only categories in the result
            bool noExpenseOnly = !filtered.Any(c => c.Type == CategoryType.Expense);

            return allCompatible
                .Label("All returned categories must have Type == Income or Type == Both")
                .And(noExpenseOnly)
                .Label("No categories with Type == Expense should appear in Income context");
        });
    }

    /// <summary>
    /// Property 5b: For Expense context, ALL returned categories have Type == Expense OR Type == Both.
    /// NO categories with Type == Income appear in the filtered result.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpenseContext_ReturnsOnly_ExpenseAndBoth()
    {
        return Prop.ForAll(CategoryListGen().ToArbitrary(), (List<CategoryDTO> categories) =>
        {
            // Act
            var filtered = CategoryPickerVM.FilterByTransactionType(categories, TransactionType.Expense);

            // Assert: every returned category must be Expense or Both
            bool allCompatible = filtered.All(c => c.Type == CategoryType.Expense || c.Type == CategoryType.Both);

            // Assert: no Income-only categories in the result
            bool noIncomeOnly = !filtered.Any(c => c.Type == CategoryType.Income);

            return allCompatible
                .Label("All returned categories must have Type == Expense or Type == Both")
                .And(noIncomeOnly)
                .Label("No categories with Type == Income should appear in Expense context");
        });
    }

    /// <summary>
    /// Property 5c: For Income context, ALL compatible categories from the input are present in the result.
    /// This verifies the filter does not accidentally drop valid Income/Both categories.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IncomeContext_IncludesAll_CompatibleCategories()
    {
        return Prop.ForAll(CategoryListGen().ToArbitrary(), (List<CategoryDTO> categories) =>
        {
            // Act
            var filtered = CategoryPickerVM.FilterByTransactionType(categories, TransactionType.Income);

            // Expected: all categories that are Income or Both
            var expected = categories.Where(c => c.Type == CategoryType.Income || c.Type == CategoryType.Both).ToList();

            bool countMatches = filtered.Count == expected.Count;

            return countMatches
                .Label($"Expected {expected.Count} compatible categories for Income context, got {filtered.Count}");
        });
    }

    /// <summary>
    /// Property 5d: For Expense context, ALL compatible categories from the input are present in the result.
    /// This verifies the filter does not accidentally drop valid Expense/Both categories.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpenseContext_IncludesAll_CompatibleCategories()
    {
        return Prop.ForAll(CategoryListGen().ToArbitrary(), (List<CategoryDTO> categories) =>
        {
            // Act
            var filtered = CategoryPickerVM.FilterByTransactionType(categories, TransactionType.Expense);

            // Expected: all categories that are Expense or Both
            var expected = categories.Where(c => c.Type == CategoryType.Expense || c.Type == CategoryType.Both).ToList();

            bool countMatches = filtered.Count == expected.Count;

            return countMatches
                .Label($"Expected {expected.Count} compatible categories for Expense context, got {filtered.Count}");
        });
    }
}
#endif
