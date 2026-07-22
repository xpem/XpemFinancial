// Feature: category-type-classification, Property 2: Subcategory inherits parent Type on creation
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
/// Property-based tests for subcategory inheriting parent Type on creation.
/// For any MainCategory with any CategoryType, a new subcategory under it has the same Type.
/// **Validates: Requirements 2.5, 5.1**
/// </summary>
[Trait("Feature", "category-type-classification")]
[Trait("Property", "2")]
public class SubcategoryTypeInheritancePropertyTests
{
    /// <summary>
    /// Generates a valid CategoryType enum member (Income, Expense, or Both).
    /// </summary>
    private static Gen<CategoryType> ValidCategoryType()
    {
        return Gen.Elements(CategoryType.Income, CategoryType.Expense, CategoryType.Both);
    }

    /// <summary>
    /// Generates a positive integer for use as ExternalId.
    /// </summary>
    private static Gen<int> PositiveExternalId()
    {
        return Gen.Choose(1, 100_000);
    }

    /// <summary>
    /// Property 2: For any MainCategory with any CategoryType value, when a new subcategory
    /// is created under that MainCategory via AddLocalAsync, the subcategory's Type property
    /// equals the parent MainCategory's Type immediately after creation.
    /// **Validates: Requirements 2.5, 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Subcategory_InheritsParentType_OnCreation()
    {
        var gen = from parentType in ValidCategoryType()
                  from parentExternalId in PositiveExternalId()
                  select (parentType, parentExternalId);

        return Prop.ForAll(
            gen.ToArbitrary(),
            async tuple =>
            {
                var (parentType, parentExternalId) = tuple;

                // Arrange: mock ICategoryRepo to return a parent with the given Type
                var categoryRepo = Substitute.For<ICategoryRepo>();
                var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
                var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

                var parentCategory = new CategoryDTO
                {
                    CategoryId = Guid.NewGuid(),
                    ExternalId = parentExternalId,
                    Name = "Parent",
                    IsMainCategory = true,
                    Type = parentType,
                    UserId = 1
                };

                categoryRepo.GetByExternalIdAsync(parentExternalId)
                    .Returns(Task.FromResult<CategoryDTO?>(parentCategory));

                var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

                // Create a subcategory under the parent
                var subcategory = new CategoryDTO
                {
                    CategoryId = Guid.Empty,
                    Name = "Subcategory",
                    IsMainCategory = false,
                    ParentExternalId = parentExternalId,
                    UserId = 1,
                    Type = CategoryType.Both // initial value doesn't matter — should be overwritten
                };

                // Act
                await service.AddLocalAsync(subcategory);

                // Assert: subcategory Type should match parent Type
                return (subcategory.Type == parentType)
                    .Label($"Expected subcategory Type={parentType} but got {subcategory.Type}");
            });
    }
}
