using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace CheaterWatcher.Api.Services;

public class StorageOptions
{
    public string DemosPath { get; set; } = "demos";
    public int MaxUploadMb { get; set; } = 500;
}

public class DemoStorage(IOptions<StorageOptions> options, IHostEnvironment env)
{
    private readonly string _root = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.DemosPath));

    public string Root => _root;

    public long MaxUploadBytes => (long)options.Value.MaxUploadMb * 1024 * 1024;

    public void EnsureRoot()
    {
        Directory.CreateDirectory(_root);
    }

    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash);
    }
}
