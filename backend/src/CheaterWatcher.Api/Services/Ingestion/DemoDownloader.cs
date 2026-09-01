using ICSharpCode.SharpZipLib.BZip2;

namespace CheaterWatcher.Api.Services.Ingestion;

public class DemoDownloader(HttpClient http, ILogger<DemoDownloader> logger)
{
    public async Task<string> DownloadDemoAsync(ShareCodeInfo info, string targetDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(targetDirectory);
        var fileName = $"match730_{info.MatchId:D21}_{info.OutcomeId:D10}_{info.TokenId:D3}.dem";
        var targetPath = Path.Combine(targetDirectory, fileName);

        if (File.Exists(targetPath))
            return targetPath;

        foreach (var url in CandidateUrls(info))
        {
            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Demo URL returned {Status}: {Url}", (int)response.StatusCode, url);
                    continue;
                }

                await using var bz2 = await response.Content.ReadAsStreamAsync(ct);
                await using var output = File.Create(targetPath);
                BZip2.Decompress(bz2, output, false);
                return targetPath;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to download demo from {Url}", url);
                TryDelete(targetPath);
            }
        }

        throw new HttpRequestException($"Could not download demo for match {info.MatchId} from any candidate URL.");
    }

    private static IEnumerable<string> CandidateUrls(ShareCodeInfo info)
    {
        var baseId = $"{info.MatchId:D21}";
        var serverHint = (int)(info.MatchId % 97) + 1;

        yield return $"https://replay{serverHint}.valve.net/730/{baseId}_{info.OutcomeId:D10}_{info.TokenId:D3}.dem.bz2";
        yield return $"https://replay{serverHint}.valve.net/730/{baseId}_{info.OutcomeId:D10}.dem.bz2";

        for (var server = 1; server <= 150; server++)
        {
            if (server == serverHint)
                continue;
            yield return $"https://replay{server}.valve.net/730/{baseId}_{info.OutcomeId:D10}_{info.TokenId:D3}.dem.bz2";
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
