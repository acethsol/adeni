namespace Adeni.Infrastructure.Tests.Persistence;

using System.Data;
using Adeni.Infrastructure.Persistence;

public sealed class StoredProcedureExecutorTests
{
    [Fact]
    public async Task QueryAsync_binds_params_maps_rows_and_reads_output()
    {
        var connection = new FakeDbConnection
        {
            ReaderFactory = command =>
            {
                var total = command.Parameters.Cast<FakeDbParameter>()
                    .Single(p => p.ParameterName == "@TotalCount");
                total.Value = 42;

                return new FakeDbDataReader(
                [
                    new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Ada" },
                    new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = "Lin" },
                ]);
            },
        };

        var result = await StoredProcedureExecutor.QueryAsync(
            connection,
            transaction: null,
            "[tenancy].[DiscoverySearch]",
            reader => (reader.GetInt32(reader.GetOrdinal("Id")), reader.GetString(reader.GetOrdinal("Name"))),
            [
                SqlParam.In("Latitude", 6.5),
                SqlParam.In("@MarketId", null),
                new SqlParam("@Note", "x", ParameterDirection.InputOutput, DbType.String, 32),
                SqlParam.Out("@TotalCount", DbType.Int32),
            ],
            CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal((1, "Ada"), result.Rows[0]);
        Assert.Equal(42, result.GetOutput<int>("@TotalCount"));
        Assert.Equal(42, result.GetOutput<int>("TotalCount"));

        var command = connection.LastCommand!;
        Assert.Equal(CommandType.StoredProcedure, command.CommandType);
        Assert.Equal("[tenancy].[DiscoverySearch]", command.CommandText);
        Assert.Equal(ConnectionState.Closed, connection.State);

        var latitude = command.Parameters.Cast<FakeDbParameter>().Single(p => p.ParameterName == "@Latitude");
        Assert.Equal(6.5, latitude.Value);
        Assert.Equal(DBNull.Value, command.Parameters.Cast<FakeDbParameter>().Single(p => p.ParameterName == "@MarketId").Value);
        Assert.Equal(32, command.Parameters.Cast<FakeDbParameter>().Single(p => p.ParameterName == "@Note").Size);
    }

    [Fact]
    public async Task QueryAsync_leaves_connection_open_when_already_open()
    {
        var connection = new FakeDbConnection
        {
            ReaderFactory = _ => new FakeDbDataReader([]),
        };
        await connection.OpenAsync();

        _ = await StoredProcedureExecutor.QueryAsync(
            connection,
            null,
            "dbo.Empty",
            _ => 0,
            [],
            CancellationToken.None);

        Assert.Equal(ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task QueryAsync_rejects_blank_procedure_name()
    {
        var connection = new FakeDbConnection();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            StoredProcedureExecutor.QueryAsync(
                connection,
                null,
                "  ",
                _ => 0,
                [],
                CancellationToken.None));
    }

    [Fact]
    public async Task QueryAsync_rejects_null_mapper()
    {
        var connection = new FakeDbConnection();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            StoredProcedureExecutor.QueryAsync<int>(
                connection,
                null,
                "dbo.P",
                null!,
                [],
                CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_returns_output_dictionary()
    {
        var connection = new FakeDbConnection
        {
            NonQueryHandler = command =>
            {
                command.Parameters.Cast<FakeDbParameter>().Single(p => p.ParameterName == "@Out").Value =
                    DBNull.Value;
                command.Parameters.Cast<FakeDbParameter>().Single(p => p.ParameterName == "@Flag").Value = 1;
            },
        };

        var outputs = await StoredProcedureExecutor.ExecuteAsync(
            connection,
            null,
            "dbo.SideEffect",
            [
                SqlParam.In("@In", "a"),
                SqlParam.Out("@Out", DbType.String, 10),
                SqlParam.Out("@Flag", DbType.Int32),
            ],
            CancellationToken.None);

        Assert.Null(outputs["@Out"]);
        Assert.Equal(1, outputs["@Flag"]);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_blank_procedure_name()
    {
        var connection = new FakeDbConnection();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            StoredProcedureExecutor.ExecuteAsync(
                connection,
                null,
                "",
                [],
                CancellationToken.None));
    }

    [Fact]
    public void GetOutput_returns_default_when_missing_or_null()
    {
        var result = new StoredProcedureResult<int>(
            [],
            new Dictionary<string, object?> { ["@Total"] = null });

        Assert.Equal(0, result.GetOutput<int>("@Missing"));
        Assert.Equal(0, result.GetOutput<int>("@Total"));
    }

    [Fact]
    public void SqlParam_helpers_set_direction()
    {
        var input = SqlParam.In("@A", 1);
        var output = SqlParam.Out("@B", DbType.Int32, 4);

        Assert.Equal(ParameterDirection.Input, input.Direction);
        Assert.Equal(1, input.Value);
        Assert.Equal(ParameterDirection.Output, output.Direction);
        Assert.Equal(DbType.Int32, output.DbType);
        Assert.Equal(4, output.Size);
    }
}
