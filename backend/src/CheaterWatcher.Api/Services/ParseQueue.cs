using System.Threading.Channels;

namespace CheaterWatcher.Api.Services;

public sealed record ParseJob(Guid MatchId, string DemoPath);

public class ParseQueue
{
    private readonly Channel<ParseJob> _channel = Channel.CreateUnbounded<ParseJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask EnqueueAsync(ParseJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<ParseJob> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
