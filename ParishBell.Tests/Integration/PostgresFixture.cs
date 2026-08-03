using Microsoft.EntityFrameworkCore;
using ParishBell.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace ParishBell.Tests.Integration;

// IMPORTANT: Mocks cannot tell whether a LINQ expression actually translates to SQL.
// NOTE: These run the real queries against a real PostgreSQL to prove it does.
// NOTE: An untranslatable projection fails here rather than as a 500 in production.
// NOTE: The schema is created from the EF model, not the production DDL.
// NOTE: That is enough to prove translation and keeps the tests self-contained.
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ParishBellDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ParishBellDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ParishBellDbContext(options);
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
