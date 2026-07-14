#if WINDOWS
using Model.DTO;
using NSubstitute;
using Service;
using Service.Account;
using Service.Category;
using Service.Recurring;
using Service.Transaction;
using Xunit;
using XpemFinancial.VMs;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Unit tests for CategoryPickerVM inactive filtering and TransactionEditVM inactive category handling.
/// Validates: Requirements 6.1, 6.2, 6.3, 6.4
/// </summary>
[Trait("Feature", "category-management")]
public class CategoryPickerVMTests
{
    private readonly ICategoryService _categoryService = Substitute.For<ICategoryService>();
    private readonly IUserSessionService _userSessionService = Substitute.For<IUserSessionService>();

    #region Requirement 6.1 — Inactive categories excluded from picker

    /// <summary>
    /// The picker filter predicate excludes categories where Inactive == true.
    /// This tests the same LINQ predicate used in CategoryPickerVM.InitializeAsync:
    /// _cachedCategories = allCategories.Where(c => !c.Inactive).ToList()
    /// Validates: Requirement 6.1
    /// </summary>
    [Fact]
    public void PickerFilterPredicate_ExcludesInactiveCategories()
    {
        // Arrange
        var activeCategory = new CategoryDTO
        {
            Id = 1, CategoryId = Guid.NewGuid(), Name = "Food",
            IsMainCategory = true, Inactive = false, UserId = 1
        };
        var inactiveCategory = new CategoryDTO
        {
            Id = 2, CategoryId = Guid.NewGuid(), Name = "Old Travel",
            IsMainCategory = true, Inactive = true, UserId = 1
        };
        var allCategories = new List<CategoryDTO> { activeCategory, inactiveCategory };

        // Act — apply the same filter as CategoryPickerVM.InitializeAsync
        var filtered = allCategories.Where(c => !c.Inactive).ToList();

        // Assert
        Assert.Contains(activeCategory, filtered);
        Assert.DoesNotContain(inactiveCategory, filtered);
        Assert.Single(filtered);
    }

    /// <summary>
    /// When all categories are inactive, the filter produces an empty list.
    /// Validates: Requirement 6.1
    /// </summary>
    [Fact]
    public void PickerFilterPredicate_AllInactive_ProducesEmptyList()
    {
        // Arrange
        var allCategories = new List<CategoryDTO>
        {
            new() { Id = 1, Name = "Archived1", IsMainCategory = true, Inactive = true, UserId = 1 },
            new() { Id = 2, Name = "Archived2", IsMainCategory = false, Inactive = true, UserId = 1, ParentExternalId = 10 }
        };

        // Act
        var filtered = allCategories.Where(c => !c.Inactive).ToList();

        // Assert
        Assert.Empty(filtered);
    }

    /// <summary>
    /// When all categories are active, all pass the filter.
    /// Validates: Requirement 6.1
    /// </summary>
    [Fact]
    public void PickerFilterPredicate_AllActive_IncludesAll()
    {
        // Arrange
        var allCategories = new List<CategoryDTO>
        {
            new() { Id = 1, Name = "Salary", IsMainCategory = true, Inactive = false, UserId = 1 },
            new() { Id = 2, Name = "Rent", IsMainCategory = true, Inactive = false, UserId = 1 }
        };

        // Act
        var filtered = allCategories.Where(c => !c.Inactive).ToList();

        // Assert
        Assert.Equal(2, filtered.Count);
    }

    /// <summary>
    /// Mixed active/inactive list: only active ones pass through.
    /// Validates: Requirement 6.1
    /// </summary>
    [Theory]
    [InlineData(3, 2, 3)] // 3 active, 2 inactive → 3 pass
    [InlineData(0, 5, 0)] // 0 active, 5 inactive → 0 pass
    [InlineData(4, 0, 4)] // 4 active, 0 inactive → 4 pass
    public void PickerFilterPredicate_MixedCategories_OnlyActivePass(int activeCount, int inactiveCount, int expectedCount)
    {
        // Arrange
        var allCategories = new List<CategoryDTO>();
        for (int i = 0; i < activeCount; i++)
            allCategories.Add(new CategoryDTO { Id = i + 1, Name = $"Active{i}", Inactive = false, UserId = 1 });
        for (int i = 0; i < inactiveCount; i++)
            allCategories.Add(new CategoryDTO { Id = activeCount + i + 1, Name = $"Inactive{i}", Inactive = true, UserId = 1 });

        // Act
        var filtered = allCategories.Where(c => !c.Inactive).ToList();

        // Assert
        Assert.Equal(expectedCount, filtered.Count);
        Assert.All(filtered, c => Assert.False(c.Inactive));
    }

    #endregion

    #region Requirement 6.2 — Active subcategories of inactive parent visible

    /// <summary>
    /// Active subcategories whose parent main category is inactive still pass the filter.
    /// The filter is by own Inactive flag only, not by parent's flag.
    /// Validates: Requirement 6.2
    /// </summary>
    [Fact]
    public void PickerFilterPredicate_ActiveSubcategoriesOfInactiveParent_AreIncluded()
    {
        // Arrange
        var inactiveParent = new CategoryDTO
        {
            Id = 1, CategoryId = Guid.NewGuid(), Name = "Travel",
            IsMainCategory = true, Inactive = true, ExternalId = 100, UserId = 1
        };
        var activeSub1 = new CategoryDTO
        {
            Id = 2, CategoryId = Guid.NewGuid(), Name = "Flights",
            IsMainCategory = false, Inactive = false, ParentExternalId = 100, UserId = 1
        };
        var activeSub2 = new CategoryDTO
        {
            Id = 3, CategoryId = Guid.NewGuid(), Name = "Hotels",
            IsMainCategory = false, Inactive = false, ParentExternalId = 100, UserId = 1
        };
        var allCategories = new List<CategoryDTO> { inactiveParent, activeSub1, activeSub2 };

        // Act — same filter as CategoryPickerVM.InitializeAsync
        var filtered = allCategories.Where(c => !c.Inactive).ToList();

        // Assert — parent excluded, subcategories included
        Assert.DoesNotContain(inactiveParent, filtered);
        Assert.Contains(activeSub1, filtered);
        Assert.Contains(activeSub2, filtered);
        Assert.Equal(2, filtered.Count);
    }

    /// <summary>
    /// Inactive subcategories of an inactive parent are also excluded (both own flag true).
    /// Validates: Requirements 6.1, 6.2
    /// </summary>
    [Fact]
    public void PickerFilterPredicate_InactiveSubcategoriesOfInactiveParent_AreExcluded()
    {
        // Arrange
        var inactiveParent = new CategoryDTO
        {
            Id = 1, Name = "Travel", IsMainCategory = true, Inactive = true, ExternalId = 100, UserId = 1
        };
        var inactiveSub = new CategoryDTO
        {
            Id = 2, Name = "Flights", IsMainCategory = false, Inactive = true, ParentExternalId = 100, UserId = 1
        };
        var activeSub = new CategoryDTO
        {
            Id = 3, Name = "Hotels", IsMainCategory = false, Inactive = false, ParentExternalId = 100, UserId = 1
        };
        var allCategories = new List<CategoryDTO> { inactiveParent, inactiveSub, activeSub };

        // Act
        var filtered = allCategories.Where(c => !c.Inactive).ToList();

        // Assert
        Assert.DoesNotContain(inactiveParent, filtered);
        Assert.DoesNotContain(inactiveSub, filtered);
        Assert.Contains(activeSub, filtered);
        Assert.Single(filtered);
    }

    /// <summary>
    /// Active parent with mix of active/inactive subcategories: parent and active subs pass, inactive subs excluded.
    /// Validates: Requirements 6.1, 6.2
    /// </summary>
    [Fact]
    public void PickerFilterPredicate_ActiveParentWithMixedSubcategories_FiltersCorrectly()
    {
        // Arrange
        var activeParent = new CategoryDTO
        {
            Id = 1, Name = "Food", IsMainCategory = true, Inactive = false, ExternalId = 200, UserId = 1
        };
        var activeSub = new CategoryDTO
        {
            Id = 2, Name = "Groceries", IsMainCategory = false, Inactive = false, ParentExternalId = 200, UserId = 1
        };
        var inactiveSub = new CategoryDTO
        {
            Id = 3, Name = "Fast Food", IsMainCategory = false, Inactive = true, ParentExternalId = 200, UserId = 1
        };
        var allCategories = new List<CategoryDTO> { activeParent, activeSub, inactiveSub };

        // Act
        var filtered = allCategories.Where(c => !c.Inactive).ToList();

        // Assert
        Assert.Contains(activeParent, filtered);
        Assert.Contains(activeSub, filtered);
        Assert.DoesNotContain(inactiveSub, filtered);
        Assert.Equal(2, filtered.Count);
    }

    #endregion

    #region Requirement 6.3 — Transaction display preserves inactive category name

    /// <summary>
    /// When editing a transaction that references an inactive category, the category name
    /// is preserved in SelectedCategoryName for display purposes.
    /// Validates: Requirement 6.3
    /// </summary>
    [Fact]
    public void ApplyQueryAttributes_InactiveCategory_PreservesCategoryNameForDisplay()
    {
        // Arrange
        var transactionService = Substitute.For<ITransactionService>();
        var accountService = Substitute.For<IAccountService>();
        var recurringRuleService = Substitute.For<IRecurringRuleService>();

        var inactiveCategory = new CategoryDTO
        {
            Id = 5, CategoryId = Guid.NewGuid(), Name = "Old Category",
            IsMainCategory = true, Inactive = true, ExternalId = 50, UserId = 1
        };
        var transaction = new TransactionDTO
        {
            Id = 10, TransactionId = Guid.NewGuid(), Description = "Test",
            Date = DateTime.Now, Amount = 100m, Type = TransactionType.Expense,
            AccountId = 1, Repetition = Repetition.None, UserId = 1,
            Category = inactiveCategory, CategoryId = 5,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        transactionService.GetByIdAsync(10).Returns(transaction);

        var vm = new TransactionEditVM(
            _userSessionService, transactionService, accountService,
            recurringRuleService, _categoryService);

        var query = new Dictionary<string, object> { { "TransactionId", "10" } };

        // Act
        vm.ApplyQueryAttributes(query);

        // Assert — inactive category name preserved for display (set synchronously)
        Assert.Equal("Old Category", vm.SelectedCategoryName);
    }

    /// <summary>
    /// When editing a transaction with an active category, SelectedCategoryName shows category name.
    /// Validates: Requirement 6.3 (counter-case: active category works normally)
    /// </summary>
    [Fact]
    public void ApplyQueryAttributes_ActiveCategory_ShowsCategoryName()
    {
        // Arrange
        var transactionService = Substitute.For<ITransactionService>();
        var accountService = Substitute.For<IAccountService>();
        var recurringRuleService = Substitute.For<IRecurringRuleService>();

        var activeCategory = new CategoryDTO
        {
            Id = 3, CategoryId = Guid.NewGuid(), Name = "Groceries",
            IsMainCategory = true, Inactive = false, ExternalId = 30, UserId = 1
        };
        var transaction = new TransactionDTO
        {
            Id = 30, TransactionId = Guid.NewGuid(), Description = "Weekly groceries",
            Date = DateTime.Now, Amount = 200m, Type = TransactionType.Expense,
            AccountId = 1, Repetition = Repetition.None, UserId = 1,
            Category = activeCategory, CategoryId = 3,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        transactionService.GetByIdAsync(30).Returns(transaction);

        var vm = new TransactionEditVM(
            _userSessionService, transactionService, accountService,
            recurringRuleService, _categoryService);

        var query = new Dictionary<string, object> { { "TransactionId", "30" } };

        // Act
        vm.ApplyQueryAttributes(query);

        // Assert
        Assert.Equal("Groceries", vm.SelectedCategoryName);
    }

    #endregion

    #region Requirement 6.4 — Picker doesn't pre-select inactive category

    /// <summary>
    /// When editing a transaction that references an inactive category,
    /// SelectedCategory is null — the picker does NOT pre-select the inactive category.
    /// Validates: Requirement 6.4
    /// </summary>
    [Fact]
    public void ApplyQueryAttributes_InactiveCategory_DoesNotPreSelectInPicker()
    {
        // Arrange
        var transactionService = Substitute.For<ITransactionService>();
        var accountService = Substitute.For<IAccountService>();
        var recurringRuleService = Substitute.For<IRecurringRuleService>();

        var inactiveCategory = new CategoryDTO
        {
            Id = 5, CategoryId = Guid.NewGuid(), Name = "Deprecated Category",
            IsMainCategory = true, Inactive = true, ExternalId = 50, UserId = 1
        };
        var transaction = new TransactionDTO
        {
            Id = 20, TransactionId = Guid.NewGuid(), Description = "Old purchase",
            Date = DateTime.Now, Amount = 50m, Type = TransactionType.Expense,
            AccountId = 1, Repetition = Repetition.None, UserId = 1,
            Category = inactiveCategory, CategoryId = 5,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        transactionService.GetByIdAsync(20).Returns(transaction);

        var vm = new TransactionEditVM(
            _userSessionService, transactionService, accountService,
            recurringRuleService, _categoryService);

        var query = new Dictionary<string, object> { { "TransactionId", "20" } };

        // Act
        vm.ApplyQueryAttributes(query);

        // Assert — SelectedCategory must be null (not pre-selected)
        Assert.Null(vm.SelectedCategory);
    }

    /// <summary>
    /// When editing a transaction with an active category, it IS pre-selected normally.
    /// Validates: Requirement 6.4 (counter-example: active category works as before)
    /// </summary>
    [Fact]
    public void ApplyQueryAttributes_ActiveCategory_IsPreSelected()
    {
        // Arrange
        var transactionService = Substitute.For<ITransactionService>();
        var accountService = Substitute.For<IAccountService>();
        var recurringRuleService = Substitute.For<IRecurringRuleService>();

        var activeCategory = new CategoryDTO
        {
            Id = 3, CategoryId = Guid.NewGuid(), Name = "Groceries",
            IsMainCategory = true, Inactive = false, ExternalId = 30, UserId = 1
        };
        var transaction = new TransactionDTO
        {
            Id = 30, TransactionId = Guid.NewGuid(), Description = "Weekly groceries",
            Date = DateTime.Now, Amount = 200m, Type = TransactionType.Expense,
            AccountId = 1, Repetition = Repetition.None, UserId = 1,
            Category = activeCategory, CategoryId = 3,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        transactionService.GetByIdAsync(30).Returns(transaction);

        var vm = new TransactionEditVM(
            _userSessionService, transactionService, accountService,
            recurringRuleService, _categoryService);

        var query = new Dictionary<string, object> { { "TransactionId", "30" } };

        // Act
        vm.ApplyQueryAttributes(query);

        // Assert — active category is pre-selected
        Assert.NotNull(vm.SelectedCategory);
        Assert.Equal("Groceries", vm.SelectedCategory.Name);
    }

    /// <summary>
    /// When user picks a new active category after seeing an inactive reference,
    /// the SelectedCategory is updated and InactiveCategoryDisplayName is cleared.
    /// Validates: Requirement 6.4
    /// </summary>
    [Fact]
    public void ApplyQueryAttributes_UserPicksNewCategory_ClearsInactiveReference()
    {
        // Arrange
        var transactionService = Substitute.For<ITransactionService>();
        var accountService = Substitute.For<IAccountService>();
        var recurringRuleService = Substitute.For<IRecurringRuleService>();

        var inactiveCategory = new CategoryDTO
        {
            Id = 5, CategoryId = Guid.NewGuid(), Name = "Old Category",
            IsMainCategory = true, Inactive = true, ExternalId = 50, UserId = 1
        };
        var transaction = new TransactionDTO
        {
            Id = 40, TransactionId = Guid.NewGuid(), Description = "Test",
            Date = DateTime.Now, Amount = 75m, Type = TransactionType.Expense,
            AccountId = 1, Repetition = Repetition.None, UserId = 1,
            Category = inactiveCategory, CategoryId = 5,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        transactionService.GetByIdAsync(40).Returns(transaction);

        var vm = new TransactionEditVM(
            _userSessionService, transactionService, accountService,
            recurringRuleService, _categoryService);

        // First load the transaction (sets inactive category state)
        var loadQuery = new Dictionary<string, object> { { "TransactionId", "40" } };
        vm.ApplyQueryAttributes(loadQuery);
        Assert.Null(vm.SelectedCategory); // inactive not pre-selected

        // Now simulate user picking a new active category from the picker
        var newCategory = new CategoryDTO
        {
            Id = 6, CategoryId = Guid.NewGuid(), Name = "New Category",
            IsMainCategory = true, Inactive = false, ExternalId = 60, UserId = 1
        };
        var pickQuery = new Dictionary<string, object>
        {
            { "SelectedCategory", newCategory },
            { "SelectedCategoryDisplayName", "New Category" }
        };

        // Act
        vm.ApplyQueryAttributes(pickQuery);

        // Assert — inactive reference cleared, new category selected
        Assert.Null(vm.InactiveCategoryDisplayName);
        Assert.False(vm.HasInactiveCategoryReference);
        Assert.Equal(newCategory, vm.SelectedCategory);
        Assert.Equal("New Category", vm.SelectedCategoryName);
    }

    /// <summary>
    /// When transaction has no category (null), SelectedCategory defaults to "Sem Categoria" placeholder.
    /// Validates: Requirement 6.4 (edge case: no category at all)
    /// </summary>
    [Fact]
    public void ApplyQueryAttributes_NullCategory_DefaultsToSemCategoria()
    {
        // Arrange
        var transactionService = Substitute.For<ITransactionService>();
        var accountService = Substitute.For<IAccountService>();
        var recurringRuleService = Substitute.For<IRecurringRuleService>();

        var transaction = new TransactionDTO
        {
            Id = 50, TransactionId = Guid.NewGuid(), Description = "No category",
            Date = DateTime.Now, Amount = 25m, Type = TransactionType.Expense,
            AccountId = 1, Repetition = Repetition.None, UserId = 1,
            Category = null, CategoryId = null,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        transactionService.GetByIdAsync(50).Returns(transaction);

        var vm = new TransactionEditVM(
            _userSessionService, transactionService, accountService,
            recurringRuleService, _categoryService);

        var query = new Dictionary<string, object> { { "TransactionId", "50" } };

        // Act
        vm.ApplyQueryAttributes(query);

        // Assert — defaults to placeholder
        Assert.NotNull(vm.SelectedCategory);
        Assert.Equal("Sem Categoria", vm.SelectedCategory.Name);
        Assert.Null(vm.InactiveCategoryDisplayName);
    }

    #endregion
}
#endif
