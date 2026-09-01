using ICSharpCode.SharpZipLib.BZip2;

namespace CheaterWatcher.Api.Services.Ingestion;

public class DemoDownloader(HttpClient http, ILogger<DemoDownloader> logger)
{
    // Adding a global wall-clock budget bounds the worst case where Valve's CDN is slow
    // to reject: previously a failed match could block a poll/upload cycle for many
    // minutes while it swept every candidate server.
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromMinutes(5);

    public async Task<string> DownloadDemoAsync(ShareCodeInfo info, string targetDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(targetDirectory);
        var fileName = $"match730_{info.MatchId:D21}_{info.OutcomeId:D10}_{info.TokenId:D3}.dem";
        var targetPath = Path.Combine(targetDirectory, fileName);
        var tmpPath = targetPath + ".tmp";

        if (File.Exists(targetPath))
        {
            // Only trust a fully-written file; a leftover .tmp from a killed process is discarded.
            TryDelete(tmpPath);
            return targetPath;
        }

        // Remove any stale partial download so a future attempt starts clean.
        TryDelete(tmpPath);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TotalTimeout);

        try
        {
            foreach (var url in CandidateUrls(info))
            {
                timeout.Token.ThrowIfCancellationRequested();

                try
                {
                    using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogInformation("Demo URL returned {Status}: {Url}", (int)response.StatusCode, url);
                        continue;
                    }

                    await using var bz2 = await response.Content.ReadAsStreamAsync(timeout.Token);
                    await using (var output = System.IO.File.Create(tmpPath))
                    {
                        BZip2.Decompress(bz2, output, false);
                    }
                    // Atomic rename: targetPath only ever appears fully written.
                    System.IO.File.Move(tmpPath, targetPath, overwrite: true);
                    return targetPath;
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    throw new HttpRequestException($"Timed out downloading demo for match {info.MatchId}.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Failed to download demo from {Url}", url);
                    TryDelete(tmpPath);
                }
            }
        }
        finally
        {
            TryDelete(tmpPath);
        }

        throw new HttpRequestException($"Could not download demo for match {info.MatchId} from any candidate URL.");
    }

    private static IEnumerable<string> CandidateUrls(ShareCodeInfo info)
    {
        var baseId = $"{info.MatchId:D21}";
        var serverHint = (int)(info.MatchId % 97) + 1;

        yield return $"https://replay{serverHint}.valve.net/730/{baseId}_{info.OutcomeId:D10}_{info.TokenId:D3}.dem.bz2";
        yield return $"https://replay{serverHint}.valve.net/730/{baseId}_{info.OutcomeId:D10}.dem.bz2";

        // A bounded fallback sweep. Trying every server 1..150 sequentially on a real miss
        // can stall the caller for many minutes and hammer Valve's CDN - a handful is enough
        // to absorb the occasional replay-server hiccup.
        var tried = 0;
        for (var server = 1; server <= 150 && tried < 8; server++)
        {
            if (server == serverHint)
                continue;
            tried++;
            yield return $"https://replay{server}.valve.net/730/{baseId}_{info.OutcomeId:D10}_{info.TokenId:D3}.dem.bz2";
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        catch
        {
        }
    }
}
