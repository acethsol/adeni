namespace Adeni.Infrastructure.Events;

using Adeni.Application.Abstractions;

public sealed class AfterCommitWorkQueue : IAfterCommitWorkQueue
{
    private readonly List<Func<CancellationToken, Task>> _pending = [];
    private int _saveDepth;

    public void EnterSave() => _saveDepth++;

    public async Task ExitSaveAsync(CancellationToken cancellationToken = default)
    {
        if (_saveDepth <= 0)
        {
            return;
        }

        _saveDepth--;
        if (_saveDepth == 0)
        {
            await FlushAsync(cancellationToken);
        }
    }

    public async Task EnqueueAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        if (_saveDepth == 0)
        {
            await work(cancellationToken);
            return;
        }

        _pending.Add(work);
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        while (_pending.Count > 0)
        {
            var batch = _pending.ToArray();
            _pending.Clear();
            foreach (var work in batch)
            {
                await work(cancellationToken);
            }
        }
    }
}
