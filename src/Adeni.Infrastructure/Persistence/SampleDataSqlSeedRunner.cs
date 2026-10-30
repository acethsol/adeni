namespace Adeni.Infrastructure.Persistence;

using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Runs idempotent T-SQL from <c>db/scripts/sample-data/</c> (Development / Staging sample seed only).
/// </summary>
public static partial class SampleDataSqlSeedRunner
{
    public const string DefaultRelativePath = "db/scripts/sample-data";

    private static readonly string[] VolumeSeedScripts =
    [
        "admin-transactions-dev-seed.sql",
        "admin-commercial-audit-dev-seed.sql",
        "seed-lekki-cuts-demo.sql",
        "seed-lekki-cuts-retention.sql",
        "seed-lekki-cuts-intake-consultation.sql",
    ];

    private static readonly string[] VolumeCleanupScripts =
    [
        "seed-lekki-cuts-retention-cleanup.sql",
        "seed-lekki-cuts-intake-consultation-cleanup.sql",
        "seed-lekki-cuts-demo-cleanup.sql",
    ];

    public static string ResolveScriptsDirectory(IHostEnvironment environment, IConfiguration configuration)
    {
        var configured = configuration["SampleData:ScriptsPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var full = Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured);
            if (Directory.Exists(full))
            {
                return Path.GetFullPath(full);
            }
        }

        string[] candidates =
        [
            Path.Combine(environment.ContentRootPath, DefaultRelativePath),
            Path.Combine(AppContext.BaseDirectory, DefaultRelativePath),
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", DefaultRelativePath)),
        ];

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new DirectoryNotFoundException(
            $"Sample data SQL folder not found. Set SampleData:ScriptsPath or deploy {DefaultRelativePath} next to the API (see Adeni.Api.csproj Content link).");
    }

    public static Task ApplyVolumeDemoAsync(
        AdeniDbContext db,
        string scriptsDirectory,
        CancellationToken cancellationToken = default) =>
        ExecuteAllAsync(db, scriptsDirectory, VolumeSeedScripts, cancellationToken);

    public static Task RemoveVolumeDemoAsync(
        AdeniDbContext db,
        string scriptsDirectory,
        CancellationToken cancellationToken = default) =>
        ExecuteAllAsync(db, scriptsDirectory, VolumeCleanupScripts, cancellationToken);

    private static async Task ExecuteAllAsync(
        AdeniDbContext db,
        string scriptsDirectory,
        IReadOnlyList<string> scriptNames,
        CancellationToken cancellationToken)
    {
        foreach (var name in scriptNames)
        {
            await ExecuteFileAsync(db, scriptsDirectory, name, cancellationToken);
        }
    }

    private static async Task ExecuteFileAsync(
        AdeniDbContext db,
        string scriptsDirectory,
        string scriptFileName,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(scriptsDirectory, scriptFileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Sample data SQL script not found: {path}");
        }

        var sql = await File.ReadAllTextAsync(path, cancellationToken);

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            foreach (var batch in SplitSqlBatches(sql))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 300;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>Splits on GO batch separators (SSMS / sqlcmd convention).</summary>
    private static IEnumerable<string> SplitSqlBatches(string script)
    {
        foreach (var part in GoBatchSeparator().Split(script))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    [GeneratedRegex(@"^\s*GO\s*(\-\-.*)?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GoBatchSeparator();
}
