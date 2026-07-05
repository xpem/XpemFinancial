using ApiRepo;
using FsCheck;
using FsCheck.Fluent;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Transaction;

namespace RecurringTests.TransferTests;

/// <summary>
/// FsCheck generators and test helpers for Transfer property-based tests.
/// Provides generators for TransactionDTO (Type == Transfer), account pairs,
/// and in-memory DbContext factory setup.
/// </summary>
public static class TransferGenerators
{
    /// <summary>
    /// Generator for a TransactionDTO with Type == Transfer and valid field invariants:
    /// - TransactionId: non-empty Guid
    /// - Amount: negative non-zero decimal
    /// - CategoryId: null
    /// - Repetition: None
    /// - DestinationAccountId != null and != AccountId
    /// - Random but valid UserId, Description, Date
    /// </summary>
    public static Gen<TransactionDTO> TransferTransaction()
    {
        return from transactionId in Gen.Fresh(() => Guid.NewGuid())
               from userId in Gen.Choose(1, 1000)
               from description in Gen.Elements("Transfer A→B", "Savings", "Wallet top-up", "Reserve", "Investment")
               from date in GenDate()
               from amountInt in Gen.Choose(1, 100_000)
               from accountId in Gen.Choose(1, 500)
               from destOffset in Gen.Choose(1, 499)
               select new TransactionDTO
               {
                   TransactionId = transactionId,
                   UserId = userId,
                   Description = description,
                   Date = date,
                   Amount = -(amountInt / 100m), // always negative non-zero
                   Type = TransactionType.Transfer,
                   CategoryId = null,
                   Repetition = Repetition.None,
                   AccountId = accountId,
                   DestinationAccountId = ((accountId + destOffset - 1) % 1000) + 1, // ensures != AccountId
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generator for a pair of AccountDTOs with distinct IDs and arbitrary CurrentBalance.
    /// The first account is the origin, the second is the destination.
    /// </summary>
    public static Gen<(AccountDTO Origin, AccountDTO Destination)> AccountPair()
    {
        return from originId in Gen.Choose(1, 500)
               from destOffset in Gen.Choose(1, 499)
               from originBalance in Gen.Choose(-100_000, 100_000)
               from destBalance in Gen.Choose(-100_000, 100_000)
               from userId in Gen.Choose(1, 1000)
               let destId = ((originId + destOffset - 1) % 1000) + 1
               select (
                   Origin: new AccountDTO
                   {
                       Id = originId,
                       Name = $"Account {originId}",
                       CurrentBalance = originBalance / 100m,
                       UserId = userId,
                       IsActive = true,
                       CreatedAt = DateTime.UtcNow,
                       UpdatedAt = DateTime.UtcNow,
                   },
                   Destination: new AccountDTO
                   {
                       Id = destId,
                       Name = $"Account {destId}",
                       CurrentBalance = destBalance / 100m,
                       UserId = userId,
                       IsActive = true,
                       CreatedAt = DateTime.UtcNow,
                       UpdatedAt = DateTime.UtcNow,
                   }
               );
    }

    /// <summary>
    /// Generates a positive non-zero transfer amount (as the user would input before negation).
    /// Returns values between 0.01 and 1000.00.
    /// </summary>
    public static Gen<decimal> PositiveAmount()
    {
        return from amountInt in Gen.Choose(1, 100_000)
               select amountInt / 100m;
    }

    /// <summary>
    /// Generator for random DateTime values in a reasonable range.
    /// </summary>
    private static Gen<DateTime> GenDate()
    {
        return from year in Gen.Choose(2020, 2025)
               from month in Gen.Choose(1, 12)
               from day in Gen.Choose(1, 28)
               select new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// Creates an IDbContextFactory backed by a unique in-memory database,
    /// optionally seeded with the provided accounts.
    /// </summary>
    public static async Task<IDbContextFactory<DbCtx>> CreateSeededFactory(
        params AccountDTO[] accounts)
    {
        var dbName = $"TransferTest_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<DbCtx>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var factory = new TestDbContextFactory(options);

        if (accounts.Length > 0)
        {
            using var ctx = await factory.CreateDbContextAsync();
            ctx.Account.AddRange(accounts);
            await ctx.SaveChangesAsync();
        }

        return factory;
    }

    /// <summary>
    /// Creates a fully configured TransactionService with in-memory DB (seeded with accounts)
    /// and NSubstitute mocks for external API dependencies.
    /// Returns the service, the factory (for verification queries), and the mocks.
    /// </summary>
    public static async Task<TransferTestContext> CreateServiceContext(
        AccountDTO origin, AccountDTO destination)
    {
        var factory = await CreateSeededFactory(origin, destination);

        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        // Configure accountRepo to return the seeded accounts
        accountRepo.GetByIdAsync(origin.Id).Returns(Task.FromResult<AccountDTO?>(origin));
        accountRepo.GetByIdAsync(destination.Id).Returns(Task.FromResult<AccountDTO?>(destination));

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        return new TransferTestContext
        {
            Service = service,
            Factory = factory,
            TransactionRepo = transactionRepo,
            TransactionApiRepo = transactionApiRepo,
            AccountRepo = accountRepo,
            CategoryRepo = categoryRepo,
            SyncCursorRepo = syncCursorRepo,
        };
    }

    /// <summary>
    /// Simple IDbContextFactory implementation for testing.
    /// </summary>
    public sealed class TestDbContextFactory(DbContextOptions<DbCtx> options) : IDbContextFactory<DbCtx>
    {
        public DbCtx CreateDbContext() => new(options);
        public Task<DbCtx> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new DbCtx(options));
    }
}

/// <summary>
/// Holds all test dependencies created by TransferGenerators.CreateServiceContext.
/// </summary>
public class TransferTestContext
{
    public required ITransactionService Service { get; init; }
    public required IDbContextFactory<DbCtx> Factory { get; init; }
    public required TransactionRepo TransactionRepo { get; init; }
    public required ITransactionApiRepo TransactionApiRepo { get; init; }
    public required IAccountRepo AccountRepo { get; init; }
    public required ICategoryRepo CategoryRepo { get; init; }
    public required ISyncCursorRepo SyncCursorRepo { get; init; }
}
