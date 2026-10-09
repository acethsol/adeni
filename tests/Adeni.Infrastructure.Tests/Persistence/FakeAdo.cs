namespace Adeni.Infrastructure.Tests.Persistence;

using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

internal sealed class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;
    private string _connectionString = "Fake";

    public FakeDbCommand? LastCommand { get; private set; }

    public Func<FakeDbCommand, DbDataReader>? ReaderFactory { get; set; }

    public Action<FakeDbCommand>? NonQueryHandler { get; set; }

    [AllowNull]
    public override string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value ?? string.Empty;
    }

    public override string Database => "Fake";

    public override string DataSource => "Fake";

    public override string ServerVersion => "1.0";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) { }

    public override void Close() => _state = ConnectionState.Closed;

    public override void Open() => _state = ConnectionState.Open;

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        Open();
        return Task.CompletedTask;
    }

    public override Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException();

    protected override DbCommand CreateDbCommand()
    {
        LastCommand = new FakeDbCommand(this)
        {
            ReaderFactory = ReaderFactory,
            NonQueryHandler = NonQueryHandler,
        };
        return LastCommand;
    }
}

internal sealed class FakeDbCommand(FakeDbConnection connection) : DbCommand
{
    private readonly FakeDbParameterCollection _parameters = new();
    private string _commandText = string.Empty;

    public Func<FakeDbCommand, DbDataReader>? ReaderFactory { get; set; }

    public Action<FakeDbCommand>? NonQueryHandler { get; set; }

    [AllowNull]
    public override string CommandText
    {
        get => _commandText;
        set => _commandText = value ?? string.Empty;
    }

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection
    {
        get => connection;
        set { }
    }

    protected override DbParameterCollection DbParameterCollection => _parameters;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }

    public override int ExecuteNonQuery()
    {
        NonQueryHandler?.Invoke(this);
        return 1;
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        NonQueryHandler?.Invoke(this);
        return Task.FromResult(1);
    }

    public override object? ExecuteScalar() => null;

    public override void Prepare() { }

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        ReaderFactory?.Invoke(this) ?? new FakeDbDataReader([]);

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
        CommandBehavior behavior,
        CancellationToken cancellationToken) =>
        Task.FromResult(ExecuteDbDataReader(behavior));
}

internal sealed class FakeDbParameter : DbParameter
{
    private string _parameterName = string.Empty;
    private string _sourceColumn = string.Empty;

    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName
    {
        get => _parameterName;
        set => _parameterName = value ?? string.Empty;
    }

    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn
    {
        get => _sourceColumn;
        set => _sourceColumn = value ?? string.Empty;
    }

    public override bool SourceColumnNullMapping { get; set; }

    public override object? Value { get; set; }

    public override void ResetDbType() { }
}

internal sealed class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<FakeDbParameter> _items = [];

    public override int Count => _items.Count;

    public override object SyncRoot => _items;

    public override int Add(object value)
    {
        _items.Add((FakeDbParameter)value);
        return _items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value!);
        }
    }

    public override void Clear() => _items.Clear();

    public override bool Contains(object value) => _items.Contains((FakeDbParameter)value);

    public override bool Contains(string value) =>
        _items.Any(p => string.Equals(p.ParameterName, value, StringComparison.OrdinalIgnoreCase));

    public override void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _items.GetEnumerator();

    public override int IndexOf(object value) => _items.IndexOf((FakeDbParameter)value);

    public override int IndexOf(string parameterName) =>
        _items.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));

    public override void Insert(int index, object value) => _items.Insert(index, (FakeDbParameter)value);

    public override void Remove(object value) => _items.Remove((FakeDbParameter)value);

    public override void RemoveAt(int index) => _items.RemoveAt(index);

    public override void RemoveAt(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
        {
            RemoveAt(index);
        }
    }

    protected override DbParameter GetParameter(int index) => _items[index];

    protected override DbParameter GetParameter(string parameterName) =>
        _items[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) =>
        _items[index] = (FakeDbParameter)value;

    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
        {
            _items[index] = (FakeDbParameter)value;
        }
        else
        {
            Add(value);
        }
    }
}

internal sealed class FakeDbDataReader(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) : DbDataReader
{
    private int _index = -1;

    private IReadOnlyDictionary<string, object?> Current => rows[_index];

    public override int FieldCount => _index < 0 ? 0 : Current.Count;

    public override int Depth => 0;

    public override bool HasRows => rows.Count > 0;

    public override bool IsClosed => false;

    public override int RecordsAffected => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => Current[name] ?? DBNull.Value;

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override char GetChar(int ordinal) => (char)GetValue(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);

    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));

    public override Type GetFieldType(int ordinal)
    {
        var value = Current.Values.ElementAt(ordinal);
        return value?.GetType() ?? typeof(DBNull);
    }

    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));

    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));

    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));

    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));

    public override string GetName(int ordinal) => Current.Keys.ElementAt(ordinal);

    public override int GetOrdinal(string name)
    {
        var index = 0;
        foreach (var key in Current.Keys)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }

            index++;
        }

        throw new IndexOutOfRangeException(name);
    }

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override object GetValue(int ordinal)
    {
        var value = Current.Values.ElementAt(ordinal);
        return value ?? DBNull.Value;
    }

    public override int GetValues(object[] values)
    {
        var i = 0;
        foreach (var value in Current.Values)
        {
            values[i++] = value ?? DBNull.Value;
        }

        return i;
    }

    public override bool IsDBNull(int ordinal)
    {
        var value = Current.Values.ElementAt(ordinal);
        return value is null or DBNull;
    }

    public override bool NextResult() => false;

    public override bool Read()
    {
        _index++;
        return _index < rows.Count;
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Read());

    public override IEnumerator GetEnumerator() => rows.GetEnumerator();
}
