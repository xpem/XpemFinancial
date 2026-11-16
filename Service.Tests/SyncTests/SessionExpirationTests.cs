using ApiRepo;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Resp.Api;
using NSubstitute;
using RecurringTests.TransferTests;
using Repo;
using Service;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Session / re-authentication behaviour. SessionMonitor is process-wide static state, so every test
/// that touches it lives in this class (xunit runs the tests of one class sequentially).
/// </summary>
public class SessionExpirationTests : IDisposable
{
    public SessionExpirationTests() => SessionMonitor.Reset();

    public void Dispose() => SessionMonitor.Reset();

    // ── SessionMonitor ────────────────────────────────────────────────────

    [Fact]
    public void MarkExpired_RaisesEventOnlyOnce_AndResetClearsIt()
    {
        int raised = 0;
        void Handler() => raised++;
        SessionMonitor.SessionExpired += Handler;

        try
        {
            SessionMonitor.MarkExpired();
            SessionMonitor.MarkExpired();

            Assert.True(SessionMonitor.IsExpired);
            Assert.Equal(1, raised);

            SessionMonitor.Reset();
            Assert.False(SessionMonitor.IsExpired);

            SessionMonitor.MarkExpired(); // a new expiration is announced again
            Assert.Equal(2, raised);
        }
        finally
        {
            SessionMonitor.SessionExpired -= Handler;
        }
    }

    [Fact]
    public void ThrowIfExpired_ThrowsTypedExceptionThatExistingHandlersStillCatch()
    {
        SessionMonitor.ThrowIfExpired(); // not expired: no-op

        SessionMonitor.MarkExpired();

        var ex = Assert.Throws<SessionExpiredException>(SessionMonitor.ThrowIfExpired);
        Assert.IsAssignableFrom<UnauthorizedAccessException>(ex);
    }

    [Fact]
    public void MarkExpired_FaultySubscriber_DoesNotBreakTheCaller()
    {
        static void Boom() => throw new InvalidOperationException("subscriber bug");
        SessionMonitor.SessionExpired += Boom;

        try
        {
            SessionMonitor.MarkExpired();
            Assert.True(SessionMonitor.IsExpired);
        }
        finally
        {
            SessionMonitor.SessionExpired -= Boom;
        }
    }

    // ── circuit breaker in the authenticated pipeline ─────────────────────

    [Fact]
    public async Task AuthRequest_WhenSessionExpired_FailsFastWithoutTouchingUserOrNetwork()
    {
        var userRepo = Substitute.For<IUserRepo>();
        var api = new UserApiRepo(userRepo);
        SessionMonitor.MarkExpired();

        await Assert.ThrowsAsync<SessionExpiredException>(() =>
            api.AuthRequestAsync(RequestsTypes.Get, "http://localhost:1/never-called"));

        await userRepo.DidNotReceive().GetAsync();
    }

    // ── sign-in: same user keeps preferences; session is un-expired ───────

    private static (UserService svc, IUserRepo repo, IBuildDbService db) UserSut(UserDTO? stored, int serverUserId)
    {
        var repo = Substitute.For<IUserRepo>();
        var userApi = Substitute.For<IUserApiRepo>();
        var db = Substitute.For<IBuildDbService>();

        repo.GetAsync().Returns(Task.FromResult(stored));
        userApi.GetAsync(Arg.Any<string>()).Returns(new ApiResp
        {
            Success = true,
            Content = $$"""{"id":{{serverUserId}},"name":"Ana","email":"ana@x.com"}""",
        });

        return (new UserService(repo, userApi, db), repo, db);
    }

    [Fact]
    public async Task SignIn_SameUser_UpdatesOnlySessionFields_KeepsLocalData_AndClearsExpiredState()
    {
        SessionMonitor.MarkExpired();
        var stored = new UserDTO { Id = 7, IncludePreviousBalance = false };
        var (svc, repo, db) = UserSut(stored, serverUserId: 7);

        var resp = await svc.SignInWithGoogleTokenAsync("tok", "ref");

        Assert.True(resp.Success);
        await repo.Received(1).UpdateSessionAsync(Arg.Is<UserDTO>(u => u.Id == 7 && u.Token == "tok" && u.RefreshToken == "ref"));
        await repo.DidNotReceive().UpdateAsync(Arg.Any<UserDTO>()); // a full update would reset preferences
        await db.DidNotReceive().CleanLocalDatabaseAsync();
        Assert.False(SessionMonitor.IsExpired);
    }

    [Fact]
    public async Task SignIn_DifferentUser_WipesLocalDatabaseBeforeAddingTheNewUser()
    {
        var stored = new UserDTO { Id = 7 };
        var (svc, repo, db) = UserSut(stored, serverUserId: 8);

        var resp = await svc.SignInWithGoogleTokenAsync("tok", "ref");

        Assert.True(resp.Success);
        await db.Received(1).CleanLocalDatabaseAsync();
        await repo.Received(1).AddAsync(Arg.Is<UserDTO>(u => u.Id == 8));
        await repo.DidNotReceive().UpdateSessionAsync(Arg.Any<UserDTO>());
    }

    [Fact]
    public async Task SignIn_NoStoredUser_AddsUser()
    {
        var (svc, repo, db) = UserSut(null, serverUserId: 3);

        await svc.SignInWithGoogleTokenAsync("tok", "ref");

        await repo.Received(1).AddAsync(Arg.Is<UserDTO>(u => u.Id == 3));
        await db.DidNotReceive().CleanLocalDatabaseAsync();
    }
}

/// <summary>Token persistence must be column-scoped (real SQLite: ExecuteUpdate is not supported in-memory).</summary>
public class UserRepoTokenTests : IAsyncLifetime
{
    private IDbContextFactory<DbCtx> _factory = null!;
    private UserRepo _repo = null!;

    public async Task InitializeAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), $"user_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<DbCtx>().UseSqlite($"Data Source={path}").Options;
        _factory = new TransferGenerators.TestDbContextFactory(options);

        using var db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        db.User.Add(new UserDTO
        {
            Id = 5,
            Name = "Old",
            Email = "old@x.com",
            Token = "old-token",
            RefreshToken = "refresh-1",
            IncludePreviousBalance = false,
            Balance = 12.5m,
        });
        await db.SaveChangesAsync();

        _repo = new UserRepo(_factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<UserDTO> Load()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.User.AsNoTracking().FirstAsync(u => u.Id == 5);
    }

    [Fact]
    public async Task UpdateTokens_ServerDidNotRotateRefreshToken_KeepsTheStoredOne()
    {
        await _repo.UpdateTokensAsync(5, "new-token", refreshToken: null);

        var user = await Load();
        Assert.Equal("new-token", user.Token);
        Assert.Equal("refresh-1", user.RefreshToken); // storing null here would sign the user out
    }

    [Fact]
    public async Task UpdateTokens_RotatedRefreshToken_IsStored_AndOtherFieldsUntouched()
    {
        await _repo.UpdateTokensAsync(5, "new-token", "refresh-2");

        var user = await Load();
        Assert.Equal("refresh-2", user.RefreshToken);
        Assert.False(user.IncludePreviousBalance);
        Assert.Equal(12.5m, user.Balance);
    }

    [Fact]
    public async Task UpdateSession_RefreshesIdentityAndTokens_ButKeepsPreferences()
    {
        await _repo.UpdateSessionAsync(new UserDTO { Id = 5, Name = "New", Email = "new@x.com", Token = "t2", RefreshToken = "r2" });

        var user = await Load();
        Assert.Equal("New", user.Name);
        Assert.Equal("new@x.com", user.Email);
        Assert.Equal("t2", user.Token);
        Assert.Equal("r2", user.RefreshToken);
        Assert.False(user.IncludePreviousBalance); // a full Update() would have reset this to true
        Assert.Equal(12.5m, user.Balance);
    }
}
