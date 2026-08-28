#if WINDOWS
using Model.DTO;
using NSubstitute;
using Service;
using Service.Category;
using Xunit;
using XpemFinancial.VMs;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Unit tests for CategoryEditVM edit mode behavior.
/// Tests: navigation with query param (5.1), pre-population of fields (5.2),
/// empty name rejection (5.3), type change blocked (5.6).
/// </summary>
[Trait("Feature", "category-management")]
public class CategoryEditVMTests
{
    private readonly ICategoryService _categoryService;
    private readonly IUserSessionService _userSessionService;

    public CategoryEditVMTests()
    {
        _categoryService = Substitute.For<ICategoryService>();
        _userSessionService = Substitute.For<IUserSessionService>();
    }

    private CategoryEditVM CreateVM() => new(_categoryService, _userSessionService);

    #region Requirement 5.1 — Navigation with query parameter

    [Fact]
    public void ApplyQueryAttributes_WithCategoryIdGuid_SetsIsEditModeTrue()
    {
        // Arrange
        var vm = CreateVM();
        var categoryId = Guid.NewGuid();
        _categoryService.GetAllAsync().Returns(Task.FromResult(new List<CategoryDTO>
        {
            new() { CategoryId = categoryId, Name = "Test", IsMainCategory = true }
        }));

        // Act
        var query = new Dictionary<string, object> { { "categoryId", categoryId } };
        vm.ApplyQueryAttributes(query);

        // Assert
        Assert.True(vm.IsEditMode);
    }

    [Fact]
    public void ApplyQueryAttributes_WithCategoryIdString_SetsIsEditModeTrue()
    {
        // Arrange
        var vm = CreateVM();
        var categoryId = Guid.NewGuid();
        _categoryService.GetAllAsync().Returns(Task.FromResult(new List<CategoryDTO>
        {
            new() { CategoryId = categoryId, Name = "Test", IsMainCategory = true }
        }));

        // Act
        var query = new Dictionary<string, object> { { "categoryId", categoryId.ToString() } };
        vm.ApplyQueryAttributes(query);

        // Assert
        Assert.True(vm.IsEditMode);
    }

    [Fact]
    public void ApplyQueryAttributes_WithoutCategoryId_RemainsInCreateMode()
    {
        // Arrange
        var vm = CreateVM();

        // Act
        var query = new Dictionary<string, object>();
        vm.ApplyQueryAttributes(query);

        // Assert
        Assert.False(vm.IsEditMode);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("12345")]
    public void ApplyQueryAttributes_WithInvalidCategoryIdString_RemainsInCreateMode(string invalidId)
    {
        // Arrange
        var vm = CreateVM();

        // Act
        var query = new Dictionary<string, object> { { "categoryId", invalidId } };
        vm.ApplyQueryAttributes(query);

        // Assert
        Assert.False(vm.IsEditMode);
    }

    #endregion

    #region Requirement 5.2 — Pre-population of fields

    [Fact]
    public async Task ApplyQueryAttributes_WithExistingMainCategory_PrePopulatesFields()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var category = new CategoryDTO
        {
            CategoryId = categoryId,
            Name = "Alimentação",
            IsMainCategory = true,
            ExternalId = 42,
            Inactive = false,
            SystemDefault = false,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _categoryService.GetAllAsync().Returns(Task.FromResult(new List<CategoryDTO> { category }));

        var vm = CreateVM();

        // Act
        var query = new Dictionary<string, object> { { "categoryId", categoryId } };
        vm.ApplyQueryAttributes(query);

        // Allow the async LoadCategoryForEditAsync to complete
        await Task.Delay(200);

        // Assert
        Assert.Equal("Alimentação", vm.Name);
        Assert.True(vm.IsMainCategory);
        Assert.Null(vm.ParentCategory);
        Assert.Null(vm.ParentCategoryName);
    }

    [Fact]
    public async Task ApplyQueryAttributes_WithExistingSubcategory_PrePopulatesFieldsWithParent()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        var parentCategory = new CategoryDTO
        {
            CategoryId = parentId,
            Name = "Alimentação",
            IsMainCategory = true,
            ExternalId = 10,
            Inactive = false,
            SystemDefault = false,
            UserId = 1,
        };

        var childCategory = new CategoryDTO
        {
            CategoryId = childId,
            Name = "Restaurantes",
            IsMainCategory = false,
            ParentExternalId = 10,
            ExternalId = 20,
            Inactive = false,
            SystemDefault = false,
            UserId = 1,
        };

        _categoryService.GetAllAsync().Returns(Task.FromResult(new List<CategoryDTO>
        {
            parentCategory, childCategory
        }));

        var vm = CreateVM();

        // Act
        var query = new Dictionary<string, object> { { "categoryId", childId } };
        vm.ApplyQueryAttributes(query);

        // Allow the async LoadCategoryForEditAsync to complete
        await Task.Delay(200);

        // Assert
        Assert.Equal("Restaurantes", vm.Name);
        Assert.False(vm.IsMainCategory);
        Assert.NotNull(vm.ParentCategory);
        Assert.Equal("Alimentação", vm.ParentCategoryName);
    }

    #endregion

    #region Requirement 5.3 — Empty name rejection

    [Fact]
    public async Task Save_WithEmptyName_DoesNotCallUpdateLocalAsync()
    {
        // Arrange
        var vm = CreateVM();
        vm.IsEditMode = true;
        vm.Name = string.Empty;

        // Act
        try
        {
            await vm.SaveCommand.ExecuteAsync(null);
        }
        catch (TypeInitializationException)
        {
            // Expected: MAUI Snackbar infrastructure not available in test env
        }

        // Assert
        await _categoryService.DidNotReceive().UpdateLocalAsync(Arg.Any<CategoryDTO>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("  \t\n  ")]
    public async Task Save_WithWhitespaceOnlyName_DoesNotCallUpdateLocalAsync(string invalidName)
    {
        // Arrange
        var vm = CreateVM();
        vm.IsEditMode = true;
        vm.Name = invalidName;

        // Act
        try
        {
            await vm.SaveCommand.ExecuteAsync(null);
        }
        catch (TypeInitializationException)
        {
            // Expected: MAUI Snackbar infrastructure not available in test env
        }

        // Assert
        await _categoryService.DidNotReceive().UpdateLocalAsync(Arg.Any<CategoryDTO>());
    }

    [Fact]
    public async Task Save_WithWhitespaceName_InCreateMode_DoesNotCallAddLocalAsync()
    {
        // Arrange
        var vm = CreateVM();
        vm.Name = "   ";

        // Act
        try
        {
            await vm.SaveCommand.ExecuteAsync(null);
        }
        catch (TypeInitializationException)
        {
            // Expected: MAUI Snackbar infrastructure not available in test env
        }

        // Assert
        await _categoryService.DidNotReceive().AddLocalAsync(Arg.Any<CategoryDTO>());
    }

    #endregion

    #region Requirement 5.6 — Type change blocked

    [Fact]
    public async Task EditMainCategoryWithActiveSubcategories_CanChangeTypeIsFalse()
    {
        // Arrange
        var mainId = Guid.NewGuid();
        var mainCategory = new CategoryDTO
        {
            CategoryId = mainId,
            Name = "Transporte",
            IsMainCategory = true,
            ExternalId = 100,
            Inactive = false,
            SystemDefault = false,
            UserId = 1,
        };
        var activeSubcategory = new CategoryDTO
        {
            CategoryId = Guid.NewGuid(),
            Name = "Uber",
            IsMainCategory = false,
            ParentExternalId = 100,
            ExternalId = 101,
            Inactive = false,
            SystemDefault = false,
            UserId = 1,
        };

        _categoryService.GetAllAsync().Returns(Task.FromResult(new List<CategoryDTO>
        {
            mainCategory, activeSubcategory
        }));

        var vm = CreateVM();

        // Act
        var query = new Dictionary<string, object> { { "categoryId", mainId } };
        vm.ApplyQueryAttributes(query);

        await Task.Delay(200);

        // Assert
        Assert.False(vm.CanChangeType);
    }

    [Fact]
    public async Task EditMainCategoryWithOnlyInactiveSubcategories_CanChangeTypeIsTrue()
    {
        // Arrange
        var mainId = Guid.NewGuid();
        var mainCategory = new CategoryDTO
        {
            CategoryId = mainId,
            Name = "Transporte",
            IsMainCategory = true,
            ExternalId = 100,
            Inactive = false,
            SystemDefault = false,
            UserId = 1,
        };
        var inactiveSubcategory = new CategoryDTO
        {
            CategoryId = Guid.NewGuid(),
            Name = "Uber",
            IsMainCategory = false,
            ParentExternalId = 100,
            ExternalId = 101,
            Inactive = true, // inactive subcategory
            SystemDefault = false,
            UserId = 1,
        };

        _categoryService.GetAllAsync().Returns(Task.FromResult(new List<CategoryDTO>
        {
            mainCategory, inactiveSubcategory
        }));

        var vm = CreateVM();

        // Act
        var query = new Dictionary<string, object> { { "categoryId", mainId } };
        vm.ApplyQueryAttributes(query);

        await Task.Delay(200);

        // Assert
        Assert.True(vm.CanChangeType);
    }

    [Fact]
    public async Task EditMainCategoryWithNoSubcategories_CanChangeTypeIsTrue()
    {
        // Arrange
        var mainId = Guid.NewGuid();
        var mainCategory = new CategoryDTO
        {
            CategoryId = mainId,
            Name = "Lazer",
            IsMainCategory = true,
            ExternalId = 200,
            Inactive = false,
            SystemDefault = false,
            UserId = 1,
        };

        _categoryService.GetAllAsync().Returns(Task.FromResult(new List<CategoryDTO>
        {
            mainCategory
        }));

        var vm = CreateVM();

        // Act
        var query = new Dictionary<string, object> { { "categoryId", mainId } };
        vm.ApplyQueryAttributes(query);

        await Task.Delay(200);

        // Assert
        Assert.True(vm.CanChangeType);
    }

    [Fact]
    public async Task EditSubcategory_CanChangeTypeDefaultsToTrue()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        var parentCategory = new CategoryDTO
        {
            CategoryId = parentId,
            Name = "Alimentação",
            IsMainCategory = true,
            ExternalId = 10,
            Inactive = false,
            SystemDefault = false,
            UserId = 1,
        };

        var childCategory = new CategoryDTO
        {
            CategoryId = childId,
            Name = "Restaurantes",
            IsMainCategory = false,
            ParentExternalId = 10,
            ExternalId = 20,
            Inactive = false,
            SystemDefault = false,
            UserId = 1,
        };

        _categoryService.GetAllAsync().Returns(Task.FromResult(new List<CategoryDTO>
        {
            parentCategory, childCategory
        }));

        var vm = CreateVM();

        // Act
        var query = new Dictionary<string, object> { { "categoryId", childId } };
        vm.ApplyQueryAttributes(query);

        await Task.Delay(200);

        // Assert — subcategories don't have CanChangeType restrictions
        Assert.True(vm.CanChangeType);
    }

    [Fact]
    public void OnIsMainCategoryChanged_WhenCanChangeTypeIsFalse_RevertsToTrue()
    {
        // Arrange
        var vm = CreateVM();
        vm.CanChangeType = false;
        vm.IsMainCategory = true; // start as main

        // Act — attempt to change to subcategory (set IsMainCategory = false)
        try
        {
            vm.IsMainCategory = false;
        }
        catch (TypeInitializationException)
        {
            // Expected: ShowMessage uses MAUI Snackbar
        }

        // Assert — should have reverted back to true
        Assert.True(vm.IsMainCategory);
    }

    #endregion
}
#endif
