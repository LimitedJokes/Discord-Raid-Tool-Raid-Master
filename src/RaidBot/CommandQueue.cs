using System.Collections.Concurrent;

namespace RaidBot;

public sealed class CommandQueue : IDisposable
{
    private readonly ConcurrentQueue<Func<Task>> _messages;
    private readonly SemaphoreSlim _signal;

    public CommandQueue()
    {
        _messages = new ConcurrentQueue<Func<Task>>();
        _signal = new SemaphoreSlim(0);
    }

    public void Queue(Func<Task> message)
    {
        _messages.Enqueue(message);
        _signal.Release();
    }

    public async Task<Func<Task>?> DequeueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _signal.WaitAsync(cancellationToken);
            _messages.TryDequeue(out var message);
            return message;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public void Dispose() => _signal.Dispose();
}
