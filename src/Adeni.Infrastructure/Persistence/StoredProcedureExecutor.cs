namespace Adeni.Infrastructure.Persistence;

using System.Data;
using System.Data.Common;

/// <summary>
/// Runs stored procedures via ADO.NET (no EF composition over EXEC/CTE).
/// SQL objects live in <c>db/Adeni.Database</c>; this is the thin call site only.
/// </summary>
internal static class StoredProcedureExecutor
{
    /// <summary>Execute a proc that returns a result set (and optional OUTPUT params).</summary>
    public static async Task<StoredProcedureResult<T>> QueryAsync<T>(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName,
        Func<DbDataReader, T> mapRow,
        IReadOnlyList<SqlParam> parameters,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
        ArgumentNullException.ThrowIfNull(mapRow);
        ArgumentNullException.ThrowIfNull(connection);

        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = CreateCommand(connection, transaction, procedureName);
            var outputNames = BindParameters(command, parameters);

            var rows = new List<T>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add(mapRow(reader));
                }
            }

            return new StoredProcedureResult<T>(rows, ReadOutputs(command, outputNames));
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>Execute a proc with no result set (OUTPUT params still returned).</summary>
    public static async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName,
        IReadOnlyList<SqlParam> parameters,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
        ArgumentNullException.ThrowIfNull(connection);

        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = CreateCommand(connection, transaction, procedureName);
            var outputNames = BindParameters(command, parameters);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return ReadOutputs(command, outputNames);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;
        return command;
    }

    private static List<string> BindParameters(DbCommand command, IReadOnlyList<SqlParam> parameters)
    {
        var outputNames = new List<string>();
        foreach (var param in parameters)
        {
            var dbParam = command.CreateParameter();
            dbParam.ParameterName = NormalizeName(param.Name);
            dbParam.Direction = param.Direction;
            if (param.DbType is { } dbType)
            {
                dbParam.DbType = dbType;
            }

            if (param.Direction is ParameterDirection.Input or ParameterDirection.InputOutput)
            {
                dbParam.Value = param.Value ?? DBNull.Value;
            }
            else
            {
                dbParam.Value = DBNull.Value;
            }

            if (param.Size is { } size)
            {
                dbParam.Size = size;
            }

            command.Parameters.Add(dbParam);
            if (param.Direction is not ParameterDirection.Input)
            {
                outputNames.Add(dbParam.ParameterName);
            }
        }

        return outputNames;
    }

    private static Dictionary<string, object?> ReadOutputs(DbCommand command, List<string> outputNames)
    {
        var outputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in outputNames)
        {
            var value = command.Parameters[name].Value;
            outputs[name] = value is DBNull ? null : value;
        }

        return outputs;
    }

    private static string NormalizeName(string name) =>
        name.StartsWith('@') ? name : "@" + name;
}

internal readonly record struct SqlParam(
    string Name,
    object? Value = null,
    ParameterDirection Direction = ParameterDirection.Input,
    DbType? DbType = null,
    int? Size = null)
{
    public static SqlParam In(string name, object? value) => new(name, value);

    public static SqlParam Out(string name, DbType dbType, int? size = null) =>
        new(name, null, ParameterDirection.Output, dbType, size);
}

internal sealed class StoredProcedureResult<T>(
    IReadOnlyList<T> rows,
    IReadOnlyDictionary<string, object?> outputs)
{
    public IReadOnlyList<T> Rows { get; } = rows;

    public IReadOnlyDictionary<string, object?> Outputs { get; } = outputs;

    public TOut GetOutput<TOut>(string name)
    {
        var key = name.StartsWith('@') ? name : "@" + name;
        if (!Outputs.TryGetValue(key, out var value) || value is null)
        {
            return default!;
        }

        return (TOut)Convert.ChangeType(value, typeof(TOut));
    }
}
