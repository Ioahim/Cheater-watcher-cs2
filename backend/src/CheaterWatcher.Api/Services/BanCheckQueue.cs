using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace CheaterWatcher.Api.Services;

public sealed record BanCheckJob(string Steam64Id);

public class BanCheckQueue
{
    private readonly Channel<BanCheckJob> _channel = Channel.CreateUnbounded<BanCheckJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask EnqueueAsync(BanCheckJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public bool TryDequeue([NotNullWhen(true)] out BanCheckJob job)
        => _channel.Reader.TryRead(out job!);
}