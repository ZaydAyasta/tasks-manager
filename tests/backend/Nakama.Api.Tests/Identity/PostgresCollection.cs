using Xunit;

namespace Nakama.Api.Tests.Identity;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresApiFactory>
{
    public const string Name = "PostgreSQL integration tests";
}
