namespace Adeni.Application.Abstractions;

/// <summary>
/// Defers work until the current EF save (and nested saves) complete successfully.
/// </summary>
public interface IAfterCommitWorkQueue
{
    void EnterSave();

    Task ExitSaveAsync(CancellationToken cancellationToken = default);

    Task EnqueueAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
}
