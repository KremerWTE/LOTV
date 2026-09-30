namespace Lotv.Api.Services.Storage;

/// <summary>Files in a local folder. For development and tests only; production uses S3.</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public bool IsConfigured => true;
    public string ProviderName => "Local";

    private string PathFor(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, StorageKeys.Require(key).Replace('/', Path.DirectorySeparatorChar)));
        // Belt and braces on top of the key rules: never leave the storage folder.
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new ArgumentException("That is not a valid storage key.", nameof(key));
        return full;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var file = File.Create(path)) await content.CopyToAsync(file, ct);
        await File.WriteAllTextAsync(path + ".contenttype", contentType, ct);
    }

    public async Task<StoredFile?> GetAsync(string key, CancellationToken ct = default)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var ctPath = path + ".contenttype";
        var contentType = File.Exists(ctPath) ? (await File.ReadAllTextAsync(ctPath, ct)).Trim() : "application/octet-stream";
        return new StoredFile(new MemoryStream(bytes), contentType, bytes.Length);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".contenttype")) File.Delete(path + ".contenttype");
        return Task.CompletedTask;
    }
}
