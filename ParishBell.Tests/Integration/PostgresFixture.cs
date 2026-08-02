using Microsoft.EntityFrameworkCore;
using ParishBell.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace ParishBell.Tests.Integration;

// IMPORTANT: Everything above the repositories is covered by mocks, which cannot tell whether a LINQ expression
// IMPORTANT:  actually translates to SQL. These tests exist for exactly that: they run the real queries against a real
// IMPORTANT:  PostgreSQL, so an untranslatable projection fails here rather than as a 500 in production.
// NOTE: The schema is created from the EF model rather than the production DDL - close enough to prove translation and
//       query logic, and it keeps the tests independent of a schema file this repo does not own.
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
