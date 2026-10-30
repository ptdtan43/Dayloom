using Dayloom.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Dayloom.IntegrationTests.Persistence;

/// <summary>
/// EF Core recognizes an applied migration only by its ID (stored in __EFMigrationsHistory), not by its content.
/// Renaming or regenerating a migration that already reached a database makes EF run it again on that database
/// ("relation already exists"). Once a migration is pushed, its ID is frozen: add it here and never change it.
/// </summary>
public class MigrationHistoryTests(DayloomApiFactory factory) : IClassFixture<DayloomApiFactory>
{
    private static readonly string[] ShippedMigrations =
    [
        "20261029220014_InitialAuthSchema", // first pushed in commit 11b3bc5 (feat/auth)
    ];

    [Fact]
    public async Task Shipped_migrations_keep_their_ids_and_order()
    {
        await using var db = factory.CreateDbContext();

        var current = db.Database.GetMigrations().ToList();

        Assert.True(
            current.Take(ShippedMigrations.Length).SequenceEqual(ShippedMigrations),
            $"Shipped migrations were renamed, removed or reordered.\nExpected to start with: {string.Join(", ", ShippedMigrations)}\nActual: {string.Join(", ", current)}");
    }

    [Fact]
    public async Task Migrating_an_up_to_date_database_again_changes_nothing()
    {
        await using var db = factory.CreateDbContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await db.Database.MigrateAsync();
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
    }
}
