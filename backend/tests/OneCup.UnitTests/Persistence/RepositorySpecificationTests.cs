using Microsoft.EntityFrameworkCore;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Persistence;

public class RepositorySpecificationTests
{
    private static async Task<(Repository<User> repo, OneCupDbContext db)> SetupAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<OneCupDbContext>().UseInMemoryDatabase(dbName).Options;
        var db = new OneCupDbContext(options);
        db.Users.AddRange(
            new User { Username = "admin", DisplayName = "管理员", Roles = new List<Role>() },
            new User { Username = "alice", DisplayName = "Alice", Roles = new List<Role>() });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (new Repository<User>(db), db);
    }

    private class UserByUsernameSpec : Specification<User>
    {
        public UserByUsernameSpec(string username) { ApplyCriteria(u => u.Username == username); ApplyInclude("Roles"); }
    }
    private class UserPagedSpec : Specification<User>
    {
        public UserPagedSpec(string? keyword, int page, int size)
        {
            if (!string.IsNullOrWhiteSpace(keyword)) ApplyCriteria(u => u.Username.Contains(keyword));
            ApplyOrderByDescending(u => u.CreatedAt);
            ApplyPaging(page, size);
        }
    }

    [Fact]
    public async Task FirstOrDefaultAsync_with_spec_finds_and_includes()
    {
        var (repo, _) = await SetupAsync(nameof(FirstOrDefaultAsync_with_spec_finds_and_includes));
        var user = await repo.FirstOrDefaultAsync(new UserByUsernameSpec("admin"), default);
        Assert.NotNull(user);
        Assert.Equal("admin", user!.Username);
    }

    [Fact]
    public async Task CountAsync_with_spec_counts_matching()
    {
        var (repo, _) = await SetupAsync(nameof(CountAsync_with_spec_counts_matching));
        var spec = new UserByUsernameSpec("admin");
        Assert.Equal(1, await repo.CountAsync(spec, default));
        Assert.Equal(2, await repo.CountAsync(null, default));
    }

    [Fact]
    public async Task ListAsync_with_paging_respects_skip_take()
    {
        var (repo, _) = await SetupAsync(nameof(ListAsync_with_paging_respects_skip_take));
        var page1 = await repo.ListAsync(new UserPagedSpec(null, 1, 1), default);
        var page2 = await repo.ListAsync(new UserPagedSpec(null, 2, 1), default);
        Assert.Single(page1);
        Assert.Single(page2);
        Assert.NotEqual(page1[0].Username, page2[0].Username);
    }

    [Fact]
    public async Task AnyAsync_with_spec()
    {
        var (repo, _) = await SetupAsync(nameof(AnyAsync_with_spec));
        Assert.True(await repo.AnyAsync(new UserByUsernameSpec("admin"), default));
        Assert.False(await repo.AnyAsync(new UserByUsernameSpec("nobody"), default));
    }
}
