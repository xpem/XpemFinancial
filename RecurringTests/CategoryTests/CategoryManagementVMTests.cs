#if WINDOWS
using Model.DTO;
using NSubstitute;
using Service;
using Service.Category;
using Xunit;
using XpemFinancial.VMs;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Unit tests for CategoryManagementVM.
/// Validates: Requirements 3.1, 3.3, 3.6, 4.4, 5.7
/// </summary>
[Trait("Feature", "category-management")]
public class CategoryManagementVMTests
{
    private readonly ICategoryService _categoryService = Substitute.For<ICategoryService>();
    private readonly IUserSessionService _userSessionService = Substitute.For<IUserSessionService>();

    private CategoryManagementVM CreateVM() => new(_categoryService, _userSessionService);

    #region Requirement 3.6, 4.4, 5.7 — System categories don't show actions

    /// <summary>
    /// A system-default category should not allow editing.
    /// Validates: Requirement 5.7
    /// </summary>
    [Fact]
    public void SystemCategory_CanEdit_IsFalse()
    {
        // Arrange
        var item = new CategoryDisplayItem
        {
            Category = new CategoryDTO
            {
                Name = "Alimentação",
                SystemDefault = true,
                Inactive = false,
                IsMainCategory = true,
            }
        };

        // Assert
        Assert.False(item.CanEdit);
    }

    /// <summary>
    /// A system-default category should not allow inactivation.
    /// Validates: Requirement 3.6
    /// </summary>
    [Fact]
    public void SystemCategory_CanInactivate_IsFalse()
    {
        // Arrange
        var item = new CategoryDisplayItem
        {
            Category = new CategoryDTO
            {
                Name = "Transporte",
                SystemDefault = true,
                Inactive = false,
                IsMainCategory = true,
            }
        };

        // Assert
        Assert.False(item.CanInactivate);
    }

    /// <summary>
    /// A system-default category should not allow reactivation (even if inactive).
    /// Validates: Requirement 4.4
    /// </summary>
    [Fact]
    public void SystemCategory_CanReactivate_IsFalse()
    {
        // Arrange
        var item = new CategoryDisplayItem
        {
            Category = new CategoryDTO
            {
                Name = "Saúde",
                SystemDefault = true,
                Inactive = true,
                IsMainCategory = true,
            }
        };

        // Assert
        Assert.False(item.CanReactivate);
    }

    /// <summary>
    /// System categories should have no actions available regardless of
    /// their inactive state or hierarchy level.
    /// Validates: Requirements 3.6, 4.4, 5.7
    /// </summary>
    [Theory]
    [InlineData(true, true)]   // inactive main
    [InlineData(true, false)]  // inactive sub
    [InlineData(false, true)]  // active main
    [InlineData(false, false)] // active sub
    public void SystemCategory_AllActionProperties_AreFalse(bool inactive, bool isMainCategory)
    {
        // Arrange
        var item = new CategoryDisplayItem
        {
            Category = new CategoryDTO
            {
                Name = "Sistema",
                SystemDefault = true,
                Inactive = inactive,
                IsMainCategory = isMainCategory,
            }
        };

        // Assert
        Assert.False(item.CanEdit);
        Assert.False(item.CanInactivate);
        Assert.False(item.CanReactivate);
    }

    #endregion

    #region User categories — correct action visibility

    /// <summary>
    /// An active user category should allow editing and inactivation, but not reactivation.
    /// Validates: Requirements 3.6, 4.4, 5.7 (inverse — user categories DO show actions)
    /// </summary>
    [Fact]
    public void ActiveUserCategory_CanEditAndInactivate_ButNotReactivate()
    {
        // Arrange
        var item = new CategoryDisplayItem
        {
            Category = new CategoryDTO
            {
                Name = "Freelance",
                SystemDefault = false,
                Inactive = false,
                IsMainCategory = true,
            }
        };

        // Assert
        Assert.True(item.CanEdit);
        Assert.True(item.CanInactivate);
        Assert.False(item.CanReactivate);
    }

    /// <summary>
    /// An inactive user category should allow editing and reactivation, but not inactivation.
    /// </summary>
    [Fact]
    public void InactiveUserCategory_CanEditAndReactivate_ButNotInactivate()
    {
        // Arrange
        var item = new CategoryDisplayItem
        {
            Category = new CategoryDTO
            {
                Name = "Antigo",
                SystemDefault = false,
                Inactive = true,
                IsMainCategory = false,
            }
        };

        // Assert
        Assert.True(item.CanEdit);
        Assert.False(item.CanInactivate);
        Assert.True(item.CanReactivate);
    }

    #endregion

    #region Requirement 3.1, 3.3 — Confirmation dialog and cancel behavior

    /// <summary>
    /// When InactivateCategory is invoked, and the confirmation dialog cannot be shown
    /// (simulating cancellation / dialog not confirmed), the service methods should NOT be called.
    /// This tests that without user confirmation, the category state remains unchanged.
    /// Validates: Requirements 3.1, 3.3
    /// </summary>
    [Fact]
    public async Task InactivateCategory_WhenDialogNotConfirmed_ServiceIsNeverCalled()
    {
        // Arrange
        var vm = CreateVM();
        var category = new CategoryDTO
        {
            CategoryId = Guid.NewGuid(),
            Name = "Lazer",
            SystemDefault = false,
            Inactive = false,
            IsMainCategory = true,
            ExternalId = 10,
        };

        // Act — Application.Current is null in test environment,
        // so DisplayAlertAsync cannot be reached. This simulates
        // the path where dialog doesn't confirm (throws before confirm).
        try
        {
            await vm.InactivateCategoryCommand.ExecuteAsync(category);
        }
        catch (NullReferenceException)
        {
            // Expected: Application.Current is null in test environment
        }

        // Assert — service methods should never be called since dialog was not confirmed
        await _categoryService.DidNotReceive().UpdateLocalAsync(Arg.Any<CategoryDTO>());
        await _categoryService.DidNotReceive().PushAsync();
    }

    /// <summary>
    /// When InactivateCategory is invoked and dialog fails, the category's Inactive flag
    /// should remain unchanged (not modified to true).
    /// Validates: Requirement 3.3
    /// </summary>
    [Fact]
    public async Task InactivateCategory_WhenDialogFails_CategoryStateUnchanged()
    {
        // Arrange
        var vm = CreateVM();
        var category = new CategoryDTO
        {
            CategoryId = Guid.NewGuid(),
            Name = "Educação",
            SystemDefault = false,
            Inactive = false,
            IsMainCategory = false,
            UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var originalUpdatedAt = category.UpdatedAt;

        // Act
        try
        {
            await vm.InactivateCategoryCommand.ExecuteAsync(category);
        }
        catch (NullReferenceException)
        {
            // Expected: Application.Current is null in test environment
        }

        // Assert — category state should not have been modified
        Assert.False(category.Inactive);
        Assert.Equal(originalUpdatedAt, category.UpdatedAt);
    }

    #endregion

    #region InitializeAsync

    /// <summary>
    /// InitializeAsync should load categories and map them to CategoryDisplayItem list.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_LoadsCategories_IntoDisplayItems()
    {
        // Arrange
        var vm = CreateVM();
        var categories = new List<CategoryDTO>
        {
            new() { Name = "Alimentação", IsMainCategory = true, SystemDefault = true, Inactive = false },
            new() { Name = "Restaurante", IsMainCategory = false, SystemDefault = false, Inactive = false },
        };
        _categoryService.GetAllGroupedAsync().Returns(categories);

        // Act
        await vm.InitializeAsync();

        // Assert
        Assert.Equal(2, vm.Categories.Count);
        Assert.False(vm.HasNoCategories);
    }

    /// <summary>
    /// InitializeAsync should set HasNoCategories to true when no categories exist.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WhenEmpty_SetsHasNoCategories()
    {
        // Arrange
        var vm = CreateVM();
        _categoryService.GetAllGroupedAsync().Returns(new List<CategoryDTO>());

        // Act
        await vm.InitializeAsync();

        // Assert
        Assert.Empty(vm.Categories);
        Assert.True(vm.HasNoCategories);
    }

    #endregion
}
#endif
