using WorkshopHub.Core.Interfaces.Repositories;

namespace WorkshopHub.Tests.Helpers;

/// <summary>
/// שער שנפתח רק כשכל המשתתפות הגיעו אליו. משמש לשחזור דטרמיניסטי של race condition:
/// כל הבקשות קוראות את אותו מצב, וכולן מחכות ממש לפני השמירה עד שהאחרונה מגיעה.
/// </summary>
internal sealed class SaveGate
{
    private readonly int _participants;
    private readonly TaskCompletionSource _everyoneArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public SaveGate(int participants)
    {
        _participants = participants;
    }

    public async Task WaitForEveryoneAsync()
    {
        if (Interlocked.Increment(ref _arrived) >= _participants)
            _everyoneArrived.TrySetResult();

        await _everyoneArrived.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }
}

/// <summary>עוטף UnitOfWork אמיתי ומעכב את השמירה עד שהשער נפתח.</summary>
internal sealed class GatedUnitOfWork : IUnitOfWork
{
    private readonly IUnitOfWork _inner;
    private readonly SaveGate _gate;

    public GatedUnitOfWork(IUnitOfWork inner, SaveGate gate)
    {
        _inner = inner;
        _gate = gate;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitForEveryoneAsync();
        return await _inner.SaveChangesAsync(cancellationToken);
    }
}
