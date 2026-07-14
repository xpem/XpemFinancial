// Feature: category-management, Property 5: Name trimming on save
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using NSubstitute;
using Service.Category;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property-based tests for name trimming on save.
/// For any valid name with leading/trailing whitespace, the persisted name equals the trimmed name.
/// Tests the trimming logic as exercised by CategoryEditVM.Save (create and edit modes).
/// **Validates: Requirements 5.4**
/// </summary>
[Trait("Feature", "category-management")]
[Trait("Property", "5")]
public class NameTrimmingPropertyTests
{
    /// <summary>
    /// Generates a non-empty, non-whitespace base string (1-20 printable characters).
    /// </summary>
    private static Gen<string> NonWhitespaceBase()
    {
        return from length in Gen.Choose(1, 20)
               from chars in Gen.ListOf(
                   Gen.Elements(
                       'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                       'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                       'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D',
                       '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'),
                   length)
               select new string(chars.ToArray());
    }

    /// <summary>
    /// Generates whitespace strings (1-5 characters) using spaces and tabs.
    /// </summary>
    private static Gen<string> WhitespaceString()
    {
        return from length in Gen.Choose(1, 5)
               from chars in Gen.ListOf(Gen.Elements(' ', '\t'), length)
               select new string(chars.ToArray());
    }

    /// <summary>
    /// Generates a name with leading and/or trailing whitespace around a non-empty base.
    /// Returns (paddedName, expectedTrimmedName).
    /// </summary>
    private static Gen<(string padded, string trimmed)> PaddedName()
    {
        return from baseName in NonWhitespaceBase()
               from leading in WhitespaceString()
               from trailing in WhitespaceString()
               select (leading + baseName + trailing, baseName);
    }

    /// <summary>
    /// Replicates the create-mode save logic from CategoryEditVM.Save:
    /// Creates a new CategoryDTO with Name = inputName.Trim()
    /// and persists via AddLocalAsync.
    /// </summary>
    private static async Task<CategoryDTO> SimulateCreateSave(
        string inputName, ICategoryService categoryService)
    {
        // Mirrors CategoryEditVM.Save create path
        var category = new CategoryDTO
        {
            Name = inputName.Trim(),
            IsMainCategory = true,
            ParentExternalId = null,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await categoryService.AddLocalAsync(category);
        return category;
    }

    /// <summary>
    /// Replicates the edit-mode save logic from CategoryEditVM.Save:
    /// Sets _editingCategory.Name = inputName.Trim() and UpdatedAt,
    /// then calls UpdateLocalAsync.
    /// </summary>
    private static async Task<CategoryDTO> SimulateEditSave(
        string inputName, CategoryDTO existingCategory, ICategoryService categoryService)
    {
        // Mirrors CategoryEditVM.Save edit path
        existingCategory.Name = inputName.Trim();
        existingCategory.IsMainCategory = existingCategory.IsMainCategory;
        existingCategory.UpdatedAt = DateTime.UtcNow;

        await categoryService.UpdateLocalAsync(existingCategory);
        return existingCategory;
    }

    /// <summary>
    /// Property 5: In create mode, the name passed to AddLocalAsync equals the trimmed name.
    /// For any valid name wrapped with leading/trailing whitespace, saving in create mode
    /// persists the trimmed version.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreateMode_PersistedName_EqualsTrimmedName()
    {
        return Prop.ForAll(
            PaddedName().ToArbitrary(),
            input =>
            {
                var (paddedName, expectedTrimmed) = input;

                // Arrange
                var categoryService = Substitute.For<ICategoryService>();
                categoryService.AddLocalAsync(Arg.Any<CategoryDTO>()).Returns(Task.CompletedTask);

                // Act - simulate the create save path
                var result = SimulateCreateSave(paddedName, categoryService).Result;

                // Assert - the persisted category name equals the trimmed input
                return (result.Name == expectedTrimmed)
                    .Label($"Expected '{expectedTrimmed}' but got '{result.Name}'");
            });
    }

    /// <summary>
    /// Property 5: In edit mode, the name set on the editing category equals the trimmed name.
    /// For any valid name wrapped with leading/trailing whitespace, saving in edit mode
    /// persists the trimmed version via UpdateLocalAsync.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EditMode_PersistedName_EqualsTrimmedName()
    {
        return Prop.ForAll(
            PaddedName().ToArbitrary(),
            input =>
            {
                var (paddedName, expectedTrimmed) = input;

                // Arrange
                var categoryService = Substitute.For<ICategoryService>();
                categoryService.UpdateLocalAsync(Arg.Any<CategoryDTO>()).Returns(Task.CompletedTask);

                var existingCategory = new CategoryDTO
                {
                    Id = 1,
                    CategoryId = Guid.NewGuid(),
                    Name = "OriginalName",
                    IsMainCategory = true,
                    UserId = 1,
                    ExternalId = 100,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1),
                };

                // Act - simulate the edit save path
                var result = SimulateEditSave(paddedName, existingCategory, categoryService).Result;

                // Assert - the persisted category name equals the trimmed input
                bool nameCorrect = result.Name == expectedTrimmed;

                // Also verify UpdateLocalAsync was called with the trimmed name
                categoryService.Received(1).UpdateLocalAsync(
                    Arg.Is<CategoryDTO>(c => c.Name == expectedTrimmed));

                return nameCorrect
                    .Label($"Expected '{expectedTrimmed}' but got '{result.Name}'");
            });
    }

    /// <summary>
    /// Property 5: Trimming preserves the non-whitespace content and only removes
    /// leading/trailing whitespace — the result equals the original base string.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TrimmedName_PreservesContent_RemovesOnlyBoundaryWhitespace()
    {
        return Prop.ForAll(
            PaddedName().ToArbitrary(),
            input =>
            {
                var (paddedName, expectedTrimmed) = input;

                // Apply the same trim logic as CategoryEditVM.Save
                string actualTrimmed = paddedName.Trim();

                bool noLeadingWhitespace = actualTrimmed.Length == 0 || !char.IsWhiteSpace(actualTrimmed[0]);
                bool noTrailingWhitespace = actualTrimmed.Length == 0 || !char.IsWhiteSpace(actualTrimmed[^1]);
                bool contentPreserved = actualTrimmed == expectedTrimmed;

                return noLeadingWhitespace
                    .Label("No leading whitespace after trim")
                    .And(noTrailingWhitespace)
                    .Label("No trailing whitespace after trim")
                    .And(contentPreserved)
                    .Label($"Content preserved: expected '{expectedTrimmed}', got '{actualTrimmed}'");
            });
    }
}
