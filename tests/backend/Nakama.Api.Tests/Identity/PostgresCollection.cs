using Xunit;

namespace Nakama.Api.Tests.Identity;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresApiFactory>
{
    public const string Name = "PostgreSQL integration tests";
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProductionPostgresCollection : ICollectionFixture<ProductionPostgresApiFactory>
{
    public const string Name = "Production PostgreSQL integration tests";
}
