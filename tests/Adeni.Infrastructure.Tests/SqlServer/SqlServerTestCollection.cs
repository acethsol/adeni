namespace Adeni.Infrastructure.Tests.SqlServer;

/// <summary>
/// SQL Server integration tests share one published database; run them serially to avoid deadlocks (1205).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerTestCollection
{
    public const string Name = "SqlServer database";
}
