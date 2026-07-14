#if WINDOWS
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NSubstitute;
using Service;
using Service.Category;
using Xunit;
using XpemFinancial.VMs;

namespace RecurringTests.CategoryTests;

// Feature: category-management, Property 4: Whitespace-only names are rejected

/// <summary>
/// Property 4: Whitespace-only names are rejected
/// For any string composed entirely of whitespace characters, save is rejected.
/// **Validates: Requirements 5.3**
/// </summary>
[Trait("Feature", "category-management")]
[Trait("Property", "4")]
public class WhitespaceNameRejectionPropertyTests
{
    /// <summary>
    /// Generates arbitrary strings composed entirely of whitespace characters
    /// (spaces, tabs, newlines, carriage returns), including the empty string.
    /// </summary>
    private static Gen<string> WhitespaceOnlyGen()
    {
        return from length in Gen.Choose(0, 20)
               from chars in Gen.ArrayOf(Gen.Elements(' ', '\t', '\n', '\r'), length)
               select new string(chars);
    }

    /// <summary>
    /// For any whitespace-only string, Save does not call AddLocalAsync (create mode).
    /// The validation rejects the name and the category is never persisted.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public FsCheck.Property WhitespaceOnlyName_InCreateMode_NeverCallsAddLocalAsync()
    {
        return Prop.ForAll(WhitespaceOnlyGen().ToArbitrary(), async (string whitespaceInput) =>
        {
            // Arrange
            var categoryService = Substitute.For<ICategoryService>();
            var userSessionService = Substitute.For<IUserSessionService>();
            var vm = new CategoryEditVM(categoryService, userSessionService);

            vm.Name = whitespaceInput;

            // Act — Save triggers validation; ShowMessage may throw in test env
            // due to MAUI infrastructure not being initialized, but the validation
            // check happens before any service call.
            try
            {
                await vm.SaveCommand.ExecuteAsync(null);
            }
            catch (TypeInitializationException)
            {
                // Expected: MAUI Snackbar infrastructure not available in test env
            }

            // Assert: AddLocalAsync is never called regardless of ShowMessage behavior
            await categoryService.DidNotReceive().AddLocalAsync(Arg.Any<Model.DTO.CategoryDTO>());
        });
    }

    /// <summary>
    /// For any whitespace-only string, Save does not call UpdateLocalAsync (edit mode).
    /// The validation rejects the name and the category is never updated.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public FsCheck.Property WhitespaceOnlyName_InEditMode_NeverCallsUpdateLocalAsync()
    {
        return Prop.ForAll(WhitespaceOnlyGen().ToArbitrary(), async (string whitespaceInput) =>
        {
            // Arrange
            var categoryService = Substitute.For<ICategoryService>();
            var userSessionService = Substitute.For<IUserSessionService>();
            var vm = new CategoryEditVM(categoryService, userSessionService);

            vm.IsEditMode = true;
            vm.Name = whitespaceInput;

            // Act — Save triggers validation; ShowMessage may throw in test env
            try
            {
                await vm.SaveCommand.ExecuteAsync(null);
            }
            catch (TypeInitializationException)
            {
                // Expected: MAUI Snackbar infrastructure not available in test env
            }

            // Assert: UpdateLocalAsync is never called regardless of ShowMessage behavior
            await categoryService.DidNotReceive().UpdateLocalAsync(Arg.Any<Model.DTO.CategoryDTO>());
        });
    }
}
#endif
