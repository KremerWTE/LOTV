namespace Lotv.Api.Services.Storage;

/// <summary>Stands in when no storage is set up: callers check <see cref="IsConfigured"/> first and refuse the upload.</summary>
public sealed class NotConfiguredFileStorage : IFileStorage
{
    public bool IsConfigured => false;
    public string ProviderName => "Not configured";

    private static InvalidOperationException NotSetUp() =>
        new("File storage is not configured. Set Storage:Provider and the S3 settings.");

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) => throw NotSetUp();
    public Task<StoredFile?> GetAsync(string key, CancellationToken ct = default) => throw NotSetUp();
    public Task DeleteAsync(string key, CancellationToken ct = default) => throw NotSetUp();
}
