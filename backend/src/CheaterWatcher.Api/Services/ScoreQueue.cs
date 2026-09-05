using System.Threading.Channels;

namespace CheaterWatcher.Api.Services;

public sealed record ScoreJob(Guid MatchId);

public class ScoreQueue
{
    private readonly Channel<ScoreJob> _channel = Channel.CreateUnbounded<ScoreJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask EnqueueAsync(ScoreJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<ScoreJob> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}