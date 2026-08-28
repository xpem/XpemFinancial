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

namespace RecurringTests.TransferTests;

/// <summary>
/// Unit tests for TransactionEditVM transfer-related UI logic.
/// Tests property state changes when transaction type is toggled to/from Transfer.
/// Validates: Requirements 2.1, 2.2, 2.3, 3.4
/// </summary>
[Trait("Feature", "transfer-transactions")]
public class TransferEditVMUnitTests
{
    private readonly IUserSessionService _userSessionService = Substitute.For<IUserSessionService>();
    private readonly ITransactionService _transactionService = Substitute.For<ITransactionService>();
    private readonly IAccountService _accountService = Substitute.For<IAccountService>();
    private readonly IRecurringRuleService _recurringRuleService = Substitute.For<IRecurringRuleService>();
    private readonly ICategoryService _categoryService = Substitute.For<ICategoryService>();

    private TransactionEditVM CreateVM() => new(
        _userSessionService,
        _transactionService,
        _accountService,
        _recurringRuleService,
        _categoryService);

    /// <summary>
    /// When SelectedTransactionType is set to Transfer, IsTransfer should become true.
    /// Validates: Requirement 2.2
    /// </summary>
    [Fact]
    public void SelectingTransferType_SetsIsTransferTrue()
    {
        // Arrange
        var vm = CreateVM();

        // Act
        vm.SelectedTransactionType = TransactionType.Transfer;

        // Assert
        Assert.True(vm.IsTransfer);
    }

    /// <summary>
    /// When SelectedTransactionType is set to a non-Transfer type (e.g., Expense),
    /// IsTransfer should be false.
    /// Validates: Requirement 2.3
    /// </summary>
    [Fact]
    public void SelectingNonTransferType_SetsIsTransferFalse()
    {
        // Arrange
        var vm = CreateVM();
        vm.SelectedTransactionType = TransactionType.Transfer; // first set to Transfer

        // Act
        vm.SelectedTransactionType = TransactionType.Expense;

        // Assert
        Assert.False(vm.IsTransfer);
    }

    /// <summary>
    /// When SelectedTransactionType is set to Transfer, InstallmentPanelIsVisible should be false.
    /// Validates: Requirement 2.2
    /// </summary>
    [Fact]
    public void SelectingTransferType_HidesInstallmentPanel()
    {
        // Arrange
        var vm = CreateVM();
        vm.SelectedRepetition = Repetition.Monthly; // would normally show installment panel
        vm.SelectedTransactionType = TransactionType.Expense;
        // Verify panel is visible before switching to Transfer
        Assert.True(vm.InstallmentPanelIsVisible);

        // Act
        vm.SelectedTransactionType = TransactionType.Transfer;

        // Assert
        Assert.False(vm.InstallmentPanelIsVisible);
    }

    /// <summary>
    /// When ActiveAccounts is set and SelectedAccount is chosen, the DestinationAccounts
    /// list should contain all active accounts EXCEPT the selected origin account.
    /// Validates: Requirement 2.1
    /// </summary>
    [Fact]
    public void FilterDestinationAccounts_ExcludesSelectedOrigin()
    {
        // Arrange
        var vm = CreateVM();
        var account1 = new AccountDTO { Id = 1, Name = "Checking", IsActive = true, UserId = 1 };
        var account2 = new AccountDTO { Id = 2, Name = "Savings", IsActive = true, UserId = 1 };
        var account3 = new AccountDTO { Id = 3, Name = "Wallet", IsActive = true, UserId = 1 };

        vm.ActiveAccounts = [account1, account2, account3];
        vm.SelectedAccount = account1;

        // Act — selecting Transfer triggers FilterDestinationAccounts
        vm.SelectedTransactionType = TransactionType.Transfer;

        // Assert
        Assert.DoesNotContain(account1, vm.DestinationAccounts);
        Assert.Contains(account2, vm.DestinationAccounts);
        Assert.Contains(account3, vm.DestinationAccounts);
        Assert.Equal(2, vm.DestinationAccounts.Count);
    }

    /// <summary>
    /// When editing a transfer, after InitializeAccountsAsync is called,
    /// SelectedDestinationAccount should be pre-selected based on the existing record.
    /// Validates: Requirement 3.4
    /// </summary>
    [Fact]
    public async Task EditingTransfer_LoadsDestinationAccountCorrectly()
    {
        // Arrange
        var vm = CreateVM();
        var account1 = new AccountDTO { Id = 1, Name = "Checking", IsActive = true, UserId = 1 };
        var account2 = new AccountDTO { Id = 2, Name = "Savings", IsActive = true, UserId = 1 };
        var account3 = new AccountDTO { Id = 3, Name = "Wallet", IsActive = true, UserId = 1 };

        var user = new UserDTO { Id = 1, Name = "Test" };
        _userSessionService.GetCurrentUserAsync().Returns(user);
        _accountService.GetActiveAsync(user.Id).Returns([account1, account2, account3]);

        // Mock the transaction loaded during ApplyQueryAttributes
        var existingTransaction = new TransactionDTO
        {
            Id = 100,
            TransactionId = Guid.NewGuid(),
            UserId = 1,
            Description = "Test transfer",
            Date = DateTime.Now,
            Amount = -50.00m,
            Type = TransactionType.Transfer,
            AccountId = 1,
            DestinationAccountId = 2,
            Repetition = Repetition.None,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _transactionService.GetByIdAsync(100).Returns(existingTransaction);

        // Simulate editing: pass TransactionId as string (navigation parameter)
        var query = new Dictionary<string, object>
        {
            { "TransactionId", "100" },
        };
        vm.ApplyQueryAttributes(query);

        // Act
        await vm.InitializeAccountsAsync();

        // Assert
        Assert.Equal(account1, vm.SelectedAccount);
        Assert.Equal(account2, vm.SelectedDestinationAccount);
    }

    /// <summary>
    /// When the selected account (origin) changes while IsTransfer is true,
    /// DestinationAccounts should be re-filtered to exclude the new origin.
    /// Validates: Requirement 2.1
    /// </summary>
    [Fact]
    public void ChangingOriginAccount_RefiltersDestinationAccounts()
    {
        // Arrange
        var vm = CreateVM();
        var account1 = new AccountDTO { Id = 1, Name = "Checking", IsActive = true, UserId = 1 };
        var account2 = new AccountDTO { Id = 2, Name = "Savings", IsActive = true, UserId = 1 };
        var account3 = new AccountDTO { Id = 3, Name = "Wallet", IsActive = true, UserId = 1 };

        vm.ActiveAccounts = [account1, account2, account3];
        vm.SelectedAccount = account1;
        vm.SelectedTransactionType = TransactionType.Transfer;

        // Verify initial filter excludes account1
        Assert.DoesNotContain(account1, vm.DestinationAccounts);

        // Act — change origin to account2
        vm.SelectedAccount = account2;

        // Assert — now account2 is excluded, account1 is back
        Assert.Contains(account1, vm.DestinationAccounts);
        Assert.DoesNotContain(account2, vm.DestinationAccounts);
        Assert.Contains(account3, vm.DestinationAccounts);
    }
}
#endif
