namespace DatabaseMultiLockBenchmark.Oracle;

using System;
using System.Threading.Tasks;

internal class AsyncAutoDisposer
    : IAsyncDisposable
{
    private readonly Func<ValueTask> _disposeAction;

    public AsyncAutoDisposer(Func<ValueTask> disposeAction)
    {
        _disposeAction = disposeAction;
    }

    public async ValueTask DisposeAsync()
    {
        await _disposeAction();
    }
}
