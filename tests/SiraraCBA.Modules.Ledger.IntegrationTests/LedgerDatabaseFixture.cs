using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Npgsql;
using SiraraCBA.BuildingBlocks.Application.Abstractions;
using SiraraCBA.Modules.Ledger.Infrastructure.Persistence.Partitions;
using SiraraCBA.Modules.Ledger.Infrastructure.Persistence;
using SiraraCBA.SharedKernel.Monetary;
using Testcontainers.PostgreSql;

namespace SiraraCBA.Modules.Ledger.IntegrationTests;

/// <summary>
/// One real PostgreSQL container per run, migrated from empty (D-13).
/// </summary>
/// <remarks>
/// Never an in-memory provider: it silently accepts what real PostgreSQL rejects,
/// and every condition in this slice is about what the database *refuses*.
/// <para>
/// Two connections, deliberately. <see cref="OwnerConnectionString"/> owns the schema
/// and runs migrations; <see cref="ApplicationConnectionString"/> connects as a
/// member of the least-privileged role. Revoking a privilege from an owner achieves
/// nothing — an owner keeps its rights — so a grant test that used the owner would
/// pass while proving nothing.
/// </para>
/// <para>
/// The application login's password is generated per run and never written down.
/// </para>
/// </remarks>
public sealed class LedgerDatabaseFixture : IAsyncLifetime
{
    private const string Image = "postgres:17-alpine";
    private const string ApplicationLogin = "sirara_ledger_app_login";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();
    private string _applicationPassword = string.Empty;

    public string OwnerConnectionString { get; private set; } = string.Empty;

    public string ApplicationConnectionString { get; private set; } = string.Empty;

    public ICurrencyRegistry Currencies { get; } = new InMemoryCurrencyRegistry(
    [
        Currency.Create("NGN", 2).Value,
        Currency.Create("JPY", 0).Value,
    ]);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        OwnerConnectionString = _container.GetConnectionString();

        await using (LedgerDbContext context = CreateOwnerContext())
        {
            // C1: from empty, unattended, in order.
            await context.Database.MigrateAsync();
        }

        _applicationPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        await ExecuteAsOwnerAsync(
            $"""
             create user "{ApplicationLogin}" password '{_applicationPassword}';
             grant "{LedgerSchema.ApplicationRole}" to "{ApplicationLogin}";
             """);

        // Every suite here seeds lines dated TestPostingDate. Without this the tests
        // only pass when the partition test happens to run first — isolation by test
        // order, which is the mistake S01's smoke suite already made once. The real
        // production routine is used, driven by a fixed clock, so the setup is
        // deterministic and exercises the thing it depends on.
        await EnsurePartitionsForTestDateAsync();

        ApplicationConnectionString =
            new NpgsqlConnectionStringBuilder(OwnerConnectionString)
            {
                Username = ApplicationLogin,
                Password = _applicationPassword,
            }.ConnectionString;
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>The posting date every suite seeds with. Fixed, so nothing drifts with the calendar.</summary>
    public static DateOnly TestPostingDate { get; } = new(2026, 3, 1);

    private async Task EnsurePartitionsForTestDateAsync()
    {
        await using LedgerDbContext context = CreateOwnerContext();

        JournalLinePartitionMaintainer maintainer = new(
            context,
            new FixedClock(new DateTimeOffset(TestPostingDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)),
            Options.Create(new PartitionOptions()));

        await maintainer.EnsurePartitionsAsync(CancellationToken.None);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;

        public DateTimeOffset UtcNow { get; }

        public DateOnly BusinessToday => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }

    public LedgerDbContext CreateOwnerContext() =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(OwnerConnectionString)
            .Options,
            Currencies);

    /// <summary>Raw SQL as the schema owner — used to set a test up, never to prove a refusal.</summary>
    public async Task ExecuteAsOwnerAsync(string sql) => await ExecuteAsync(OwnerConnectionString, sql);

    /// <summary>Raw SQL as the least-privileged application role.</summary>
    public async Task ExecuteAsApplicationAsync(string sql) => await ExecuteAsync(ApplicationConnectionString, sql);

    /// <summary>A count. Postgres never returns null for count(*), so this is non-nullable.</summary>
    public async Task<long> CountAsOwnerAsync(string sql)
    {
        await using NpgsqlConnection connection = new(OwnerConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<T?> ScalarAsOwnerAsync<T>(string sql)
    {
        await using NpgsqlConnection connection = new(OwnerConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        object? value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class LedgerDatabaseSuite : ICollectionFixture<LedgerDatabaseFixture>
{
    public const string Name = "ledger-database";
}
