using System.Threading.Channels;

namespace PulseCheck.Agent;

public sealed class SyncWakeSignal
{
    private readonly Channel<bool> channel = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public void Signal()
    {
        channel.Writer.TryWrite(true);
    }

    public async Task<bool> WaitForNextAsync(TimeSpan fallbackDelay, CancellationToken cancellationToken)
    {
        var readTask = channel.Reader.ReadAsync(cancellationToken).AsTask();
        var delayTask = Task.Delay(fallbackDelay, cancellationToken);

        var completed = await Task.WhenAny(readTask, delayTask);
        if (completed == readTask)
        {
            while (channel.Reader.TryRead(out _))
            {
            }

            return true;
        }

        return false;
    }
}
