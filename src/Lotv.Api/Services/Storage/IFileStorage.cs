namespace Lotv.Api.Services.Storage;

/// <summary>A stored file read back into memory (uploads are capped at a few MB, so this stays small).</summary>
public sealed record StoredFile(Stream Content, string ContentType, long Length);

/// <summary>
/// Where uploaded files (profile photos, receipts, documents) live. Amazon S3 in production; a local folder for
/// development and tests. When nothing is set up (production without S3 settings) <see cref="IsConfigured"/> is false
/// and uploads are refused, so files are never quietly written to the web server's own disk.
/// </summary>
public interface IFileStorage
{
    bool IsConfigured { get; }

    /// <summary>"S3", "Local" or "Not configured", for diagnostics.</summary>
    string ProviderName { get; }

    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>The file, or null when there is nothing under that key.</summary>
    Task<StoredFile?> GetAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}

/// <summary>Storage keys are plain relative paths: letters, digits, dash, underscore, dot and slash only.</summary>
public static class StorageKeys
{
    private const int MaxLength = 220;

    public static bool IsValid(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxLength) return false;
        if (key.StartsWith('/') || key.EndsWith('/') || key.Contains("//") || key.Contains("..")) return false;
        foreach (var c in key)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '/')) return false;
        return true;
    }

    public static string Require(string? key) =>
        IsValid(key) ? key! : throw new ArgumentException("That is not a valid storage key.", nameof(key));

    /// <summary>A file name reduced to something safe to put inside a key.</summary>
    public static string SafeName(string? name)
    {
        var stem = Path.GetFileNameWithoutExtension(name ?? "") ?? "";
        var ext = Path.GetExtension(name ?? "").TrimStart('.').ToLowerInvariant();
        var cleaned = new string(stem.Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
        if (cleaned.Length == 0) cleaned = "file";
        if (cleaned.Length > 60) cleaned = cleaned[..60];
        var cleanExt = new string(ext.Where(char.IsAsciiLetterOrDigit).ToArray());
        return cleanExt.Length == 0 ? cleaned : $"{cleaned}.{cleanExt[..Math.Min(cleanExt.Length, 8)]}";
    }
}
