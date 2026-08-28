using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Service.Transaction;
using ApiRepo;
using Xunit;
using NSubstitute;

namespace RecurringTests.RecurringDeletionTests
{
    public class DeleteFutureOccurrencesTests : IAsyncLifetime
    {
        private IDbContextFactory<DbCtx> _dbFactory = null!;
        private TransactionService _transactionService = null!;
        private TransactionRepo _transactionRepo = null!;
        private AccountRepo _accountRepo = null!;
        private CategoryRepo _categoryRepo = null!;
        private ISyncCursorRepo _syncCursorRepo = null!;
        private ITransactionApiRepo _transactionApiRepo = null!;

        public async Task InitializeAsync()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
            var options = new DbContextOptionsBuilder<DbCtx>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            _dbFactory = new TestDbContextFactory(options);

            using var db = await _dbFactory.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();

            _transactionRepo = new TransactionRepo(_dbFactory);
            _accountRepo = new AccountRepo(_dbFactory);
            _categoryRepo = new CategoryRepo(_dbFactory);
            _syncCursorRepo = Substitute.For<ISyncCursorRepo>();
            _transactionApiRepo = Substitute.For<ITransactionApiRepo>();

            _transactionService = new TransactionService(_transactionRepo, _transactionApiRepo, _categoryRepo, _accountRepo, _syncCursorRepo);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task<(int userId, int accountId)> SeedUserAndAccount()
        {
            using var db = await _dbFactory.CreateDbContextAsync();
            
            var user = new UserDTO
            {
                Email = "test@test.com",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.User.Add(user);
            await db.SaveChangesAsync();

            var account = new AccountDTO
            {
                Name = "Test Account",
                UserId = user.Id,
                CurrentBalance = 0,
                AccountId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Account.Add(account);
            await db.SaveChangesAsync();

            return (user.Id, account.Id);
        }

        [Fact]
        public async Task DeleteFutureOccurrences_ShouldPreservePastMonths()
        {
            // Arrange: Criar 4 ocorrências de uma regra recorrente
            var (userId, accountId) = await SeedUserAndAccount();
            var recurringRuleId = Guid.NewGuid();

            var january = new TransactionDTO
            {
                TransactionId = Guid.NewGuid(),
                Description = "Aluguel",
                Date = new DateTime(2024, 1, 10),
                Amount = -1000,
                Type = TransactionType.Expense,
                RecurringRuleId = recurringRuleId,
                Repetition = Repetition.Recurring,
                AccountId = accountId,
                UserId = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var february = new TransactionDTO
            {
                TransactionId = Guid.NewGuid(),
                Description = "Aluguel",
                Date = new DateTime(2024, 2, 10),
                Amount = -1000,
                Type = TransactionType.Expense,
                RecurringRuleId = recurringRuleId,
                Repetition = Repetition.Recurring,
                AccountId = accountId,
                UserId = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var march = new TransactionDTO
            {
                TransactionId = Guid.NewGuid(),
                Description = "Aluguel",
                Date = new DateTime(2024, 3, 10),
                Amount = -1000,
                Type = TransactionType.Expense,
                RecurringRuleId = recurringRuleId,
                Repetition = Repetition.Recurring,
                AccountId = accountId,
                UserId = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var april = new TransactionDTO
            {
                TransactionId = Guid.NewGuid(),
                Description = "Aluguel",
                Date = new DateTime(2024, 4, 10),
                Amount = -1000,
                Type = TransactionType.Expense,
                RecurringRuleId = recurringRuleId,
                Repetition = Repetition.Recurring,
                AccountId = accountId,
                UserId = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _transactionRepo.Add(january);
            await _transactionRepo.Add(february);
            await _transactionRepo.Add(march);
            await _transactionRepo.Add(april);

            // Act: Excluir de março em diante
            await _transactionService.DeleteFutureOccurrencesAsync(recurringRuleId, new DateTime(2024, 3, 10));

            // Assert: Janeiro e Fevereiro devem permanecer ativos
            var allOccurrences = await _transactionService.GetByRecurringRuleIdAsync(recurringRuleId);
            var activeOccurrences = allOccurrences.Where(t => !t.Inactive).ToList();
            var inactiveOccurrences = allOccurrences.Where(t => t.Inactive).ToList();

            Assert.Equal(2, activeOccurrences.Count);
            Assert.Equal(2, inactiveOccurrences.Count);

            Assert.Contains(activeOccurrences, t => t.Date.Month == 1);
            Assert.Contains(activeOccurrences, t => t.Date.Month == 2);
            Assert.Contains(inactiveOccurrences, t => t.Date.Month == 3);
            Assert.Contains(inactiveOccurrences, t => t.Date.Month == 4);
        }

        [Fact]
        public async Task DeleteFutureOccurrences_WithAlreadyDeletedPastOccurrence_ShouldNotReactivate()
        {
            // Arrange: Criar ocorrências onde fevereiro já foi excluído manualmente
            var (userId, accountId) = await SeedUserAndAccount();
            var recurringRuleId = Guid.NewGuid();

            var january = new TransactionDTO
            {
                TransactionId = Guid.NewGuid(),
                Description = "Aluguel",
                Date = new DateTime(2024, 1, 10),
                Amount = -1000,
                Type = TransactionType.Expense,
                RecurringRuleId = recurringRuleId,
                Repetition = Repetition.Recurring,
                AccountId = accountId,
                UserId = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Inactive = false
            };

            var february = new TransactionDTO
            {
                TransactionId = Guid.NewGuid(),
                Description = "Aluguel",
                Date = new DateTime(2024, 2, 10),
                Amount = -1000,
                Type = TransactionType.Expense,
                RecurringRuleId = recurringRuleId,
                Repetition = Repetition.Recurring,
                AccountId = accountId,
                UserId = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Inactive = true  // Já foi excluída anteriormente
            };

            var march = new TransactionDTO
            {
                TransactionId = Guid.NewGuid(),
                Description = "Aluguel",
                Date = new DateTime(2024, 3, 10),
                Amount = -1000,
                Type = TransactionType.Expense,
                RecurringRuleId = recurringRuleId,
                Repetition = Repetition.Recurring,
                AccountId = accountId,
                UserId = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Inactive = false
            };

            await _transactionRepo.Add(january);
            await _transactionRepo.Add(february);
            await _transactionRepo.Add(march);

            // Act: Excluir de março em diante
            await _transactionService.DeleteFutureOccurrencesAsync(recurringRuleId, new DateTime(2024, 3, 10));

            // Assert: Fevereiro deve continuar inativo (não reativado)
            var allOccurrences = await _transactionService.GetByRecurringRuleIdAsync(recurringRuleId);
            
            var jan = allOccurrences.First(t => t.Date.Month == 1);
            var feb = allOccurrences.First(t => t.Date.Month == 2);
            var mar = allOccurrences.First(t => t.Date.Month == 3);

            Assert.False(jan.Inactive);
            Assert.True(feb.Inactive);
            Assert.True(mar.Inactive);
        }

        [Fact]
        public async Task DeleteFutureOccurrences_EntireRule_ShouldDeleteAllOccurrences()
        {
            // Arrange
            var (userId, accountId) = await SeedUserAndAccount();
            var recurringRuleId = Guid.NewGuid();

            var january = new TransactionDTO
            {
                TransactionId = Guid.NewGuid(),
                Description = "Aluguel",
                Date = new DateTime(2024, 1, 10),
                Amount = -1000,
                Type = TransactionType.Expense,
                RecurringRuleId = recurringRuleId,
                Repetition = Repetition.Recurring,
                AccountId = accountId,
                UserId = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var march = new TransactionDTO
            {
                TransactionId = Guid.NewGuid(),
                Description = "Aluguel",
                Date = new DateTime(2024, 3, 10),
                Amount = -1000,
                Type = TransactionType.Expense,
                RecurringRuleId = recurringRuleId,
                Repetition = Repetition.Recurring,
                AccountId = accountId,
                UserId = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _transactionRepo.Add(january);
            await _transactionRepo.Add(march);

            // Act: Excluir todas (desde DateTime.MinValue)
            await _transactionService.DeleteFutureOccurrencesAsync(recurringRuleId, DateTime.MinValue);

            // Assert: Todas devem estar inativas
            var allOccurrences = await _transactionService.GetByRecurringRuleIdAsync(recurringRuleId);
            
            Assert.All(allOccurrences, t => Assert.True(t.Inactive));
        }
    }

    // Helper class para criar DbContext em testes
    public class TestDbContextFactory : IDbContextFactory<DbCtx>
    {
        private readonly DbContextOptions<DbCtx> _options;

        public TestDbContextFactory(DbContextOptions<DbCtx> options)
        {
            _options = options;
        }

        public DbCtx CreateDbContext()
        {
            return new DbCtx(_options);
        }

        public Task<DbCtx> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DbCtx(_options));
        }
    }
}
