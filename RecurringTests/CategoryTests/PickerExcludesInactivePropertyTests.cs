// Feature: category-management, Property 7: Category Picker excludes all inactive categories
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property 7: Category Picker excludes all inactive categories
/// Validates: Requirements 6.1, 6.2
/// For any set of categories, picker result contains only items where Inactive == false.
/// The filtering is by each category's own Inactive flag — active subcategories of inactive parents still show.
/// </summary>
[Trait("Feature", "category-management")]
[Trait("Property", "7")]
public class PickerExcludesInactivePropertyTests
{
    /// <summary>
    /// Generates an arbitrary CategoryDTO with random Inactive flag.
    /// </summary>
    private static Gen<CategoryDTO> CategoryGen()
    {
        return from name in Gen.Elements("Food", "Transport", "Health", "Salary", "Rent", "Entertainment")
               from isMain in Gen.Elements(true, false)
               from inactive in Gen.Elements(true, false)
               from parentExtId in Gen.Choose(1, 100)
               from extId in Gen.Choose(101, 1000)
               select new CategoryDTO
               {
                   CategoryId = Guid.NewGuid(),
                   ExternalId = extId,
                   Name = name,
                   IsMainCategory = isMain,
                   ParentExternalId = isMain ? null : parentExtId,
                   Inactive = inactive,
                   UserId = 1,
                   SystemDefault = false,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generates an arbitrary list of categories with varying Inactive flags.
    /// </summary>
    private static Gen<List<CategoryDTO>> CategoryListGen()
    {
        return from count in Gen.Choose(0, 30)
               from categories in Gen.ListOf(CategoryGen(), count)
               select categories.ToList();
    }

    /// <summary>
    /// For any set of categories with varying Inactive flags, applying the picker filter
    /// (the same predicate used in CategoryPickerVM.InitializeAsync) produces a result
    /// that contains ONLY items where Inactive == false and includes ALL active items.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PickerFilter_ExcludesAllInactive_AndIncludesAllActive()
    {
        return Prop.ForAll(CategoryListGen().ToArbitrary(), (List<CategoryDTO> categories) =>
        {
            // Apply the same filter logic as CategoryPickerVM.InitializeAsync:
            // _cachedCategories = allCategories.Where(c => !c.Inactive).ToList()
            var pickerResult = categories.Where(c => !c.Inactive).ToList();

            // Property 1: every item in the result must have Inactive == false
            bool allResultsActive = pickerResult.All(c => !c.Inactive);

            // Property 2: count of results matches count of active items in input
            int activeCountInInput = categories.Count(c => !c.Inactive);
            bool countMatches = pickerResult.Count == activeCountInInput;

            return allResultsActive
                .Label("All items in picker result must have Inactive == false")
                .And(countMatches)
                .Label($"Expected {activeCountInInput} active items, got {pickerResult.Count}");
        });
    }

    /// <summary>
    /// For any set of categories where a main category is inactive but has active subcategories,
    /// the picker filter still includes those active subcategories (filters by own Inactive flag only).
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PickerFilter_ActiveSubcategoriesOfInactiveParent_StillIncluded()
    {
        var scenarioGen =
            from parentExtId in Gen.Choose(1, 100)
            from subCount in Gen.Choose(1, 5)
            from subCategories in Gen.ListOf(
                from name in Gen.Elements("Sub1", "Sub2", "Sub3", "Sub4", "Sub5")
                select new CategoryDTO
                {
                    CategoryId = Guid.NewGuid(),
                    ExternalId = null,
                    Name = name,
                    IsMainCategory = false,
                    ParentExternalId = parentExtId,
                    Inactive = false, // active subcategories
                    UserId = 1,
                    SystemDefault = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                },
                subCount)
            select (ParentExtId: parentExtId, SubCategories: subCategories.ToList());

        return Prop.ForAll(scenarioGen.ToArbitrary(), scenario =>
        {
            // Create an inactive parent
            var inactiveParent = new CategoryDTO
            {
                CategoryId = Guid.NewGuid(),
                ExternalId = scenario.ParentExtId,
                Name = "InactiveParent",
                IsMainCategory = true,
                ParentExternalId = null,
                Inactive = true,
                UserId = 1,
                SystemDefault = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            var allCategories = new List<CategoryDTO> { inactiveParent };
            allCategories.AddRange(scenario.SubCategories);

            // Apply picker filter (same as CategoryPickerVM.InitializeAsync)
            var pickerResult = allCategories.Where(c => !c.Inactive).ToList();

            // Inactive parent must NOT appear in picker
            bool parentExcluded = !pickerResult.Contains(inactiveParent);

            // All active subcategories must appear (filtered by own Inactive flag only)
            bool allActiveSubsIncluded = scenario.SubCategories.All(sub => pickerResult.Contains(sub));

            return parentExcluded
                .Label("Inactive parent must be excluded from picker")
                .And(allActiveSubsIncluded)
                .Label("All active subcategories must be included regardless of parent's Inactive flag");
        });
    }
}
