using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AiAgents.Setup;

public interface ISearchBlobStore
{
    string Identity { get; }
    Task CheckPrivateAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);
    Task<string> CreateAsync(string name, byte[] content, CancellationToken cancellationToken);
    Task<bool> MatchesAsync(string name, string etag, byte[] content, CancellationToken cancellationToken);
}

public sealed class SearchAzureBlobStore : ISearchBlobStore
{
    private readonly BlobContainerClient _container;

    public SearchAzureBlobStore(Uri containerUri, TokenCredential credential, HttpClient? http = null)
    {
        ArgumentNullException.ThrowIfNull(containerUri);
        ArgumentNullException.ThrowIfNull(credential);
        const string suffix = ".blob.core.windows.net";
        string host = containerUri.IsAbsoluteUri ? containerUri.Host : string.Empty;
        string account = host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? host[..^suffix.Length] : string.Empty;
        string path = containerUri.IsAbsoluteUri ? containerUri.AbsolutePath : string.Empty;
        string container = path.StartsWith('/') ? path[1..] : string.Empty;
        if (!containerUri.IsAbsoluteUri || containerUri.Scheme != Uri.UriSchemeHttps ||
            !containerUri.IsDefaultPort || account.Length is < 3 or > 24 ||
            !account.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character)) ||
            containerUri.UserInfo.Length != 0 || containerUri.Query.Length != 0 ||
            containerUri.Fragment.Length != 0 || container.Length is < 3 or > 63 ||
            !char.IsAsciiLetterLower(container[0]) && !char.IsAsciiDigit(container[0]) ||
            !char.IsAsciiLetterLower(container[^1]) && !char.IsAsciiDigit(container[^1]) ||
            !container.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-') ||
            container.Contains("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("A public-cloud HTTPS Blob container URI without credentials is required.", nameof(containerUri));
        }

        var options = new BlobClientOptions();
        options.Retry.MaxRetries = 0;
        if (http is not null)
        {
            options.Transport = new HttpClientTransport(http);
        }
        _container = new BlobContainerClient(containerUri, credential, options);
        Identity = _container.Uri.AbsoluteUri.TrimEnd('/');
    }

    public string Identity { get; }

    public async Task CheckPrivateAsync(CancellationToken cancellationToken)
    {
        var properties = await _container.GetPropertiesAsync(cancellationToken: cancellationToken);
        if (properties.Value.PublicAccess != PublicAccessType.None)
        {
            throw new InvalidDataException("The Search corpus container must not allow public access.");
        }
    }

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
    {
        List<string> names = [];
        await foreach (var blob in _container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            names.Add(blob.Name);
            if (names.Count > 40)
            {
                throw new InvalidDataException("The Search corpus container contains more Blobs than the approved manifest limit.");
            }
        }

        return names;
    }

    public async Task<string> CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
    {
        var blob = _container.GetBlobClient(name);
        using var stream = new MemoryStream(content, writable: false);
        var response = await blob.UploadAsync(stream, new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = name.EndsWith(".pdf", StringComparison.Ordinal) ? "application/pdf" : "text/markdown"
            }
        }, cancellationToken);
        return response.Value.ETag.ToString();
    }

    public async Task<bool> MatchesAsync(string name, string etag, byte[] content, CancellationToken cancellationToken)
    {
        var blob = _container.GetBlobClient(name);
        var conditions = new BlobRequestConditions { IfMatch = new ETag(etag) };
        var properties = await blob.GetPropertiesAsync(conditions, cancellationToken);
        if (properties.Value.ContentLength != content.Length)
        {
            return false;
        }

        var download = await blob.DownloadContentAsync(new BlobDownloadOptions { Conditions = conditions }, cancellationToken);
        return download.Value.Content.ToMemory().Span.SequenceEqual(content);
    }
}

public sealed class SearchBlobUpload(ISearchBlobStore store)
{
    public async Task UploadAsync(string markdownManifest, string pdfManifest, string statePath, CancellationToken cancellationToken)
    {
        var products = await SearchCorpus.LoadAsync(markdownManifest, pdfManifest, cancellationToken);
        var files = products.Files.ToDictionary(
            file => (file.Name.EndsWith(".pdf", StringComparison.Ordinal) ? "pdf/" : "markdown/") + file.Name,
            file => file.Content.ToArray(), StringComparer.Ordinal);
        statePath = Path.GetFullPath(statePath);
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        await using var stateLock = new FileStream(
            statePath + ".lock", FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);

        State state;
        if (File.Exists(statePath))
        {
            await using var input = File.OpenRead(statePath);
            state = await JsonSerializer.DeserializeAsync<State>(input, ProductManifest.JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("The Search upload state is empty.");
            if (state.Kind != "search-blob-upload-v1" || state.ManifestHash != products.ManifestHash ||
                state.Container != store.Identity || state.Files is null ||
                state.Files.Any(pair => !files.ContainsKey(pair.Key) || pair.Value is null ||
                    string.IsNullOrWhiteSpace(pair.Value.ETag) ||
                    pair.Value.Sha256 != Convert.ToHexString(SHA256.HashData(files[pair.Key]))))
            {
                throw new InvalidDataException("Search upload state does not match the container and verified corpus; do not overwrite it.");
            }

            if (state.Pending is not null)
            {
                throw new InvalidDataException("A previous Blob write has an unknown outcome; reconcile it before proceeding.");
            }
        }
        else
        {
            state = new State { Kind = "search-blob-upload-v1", ManifestHash = products.ManifestHash, Container = store.Identity };
        }

        await store.CheckPrivateAsync(cancellationToken);
        await VerifyMembershipAsync(state.Files.Keys, cancellationToken);
        foreach (var (name, checkpoint) in state.Files)
        {
            if (!await store.MatchesAsync(name, checkpoint.ETag, files[name], cancellationToken))
            {
                throw new InvalidDataException("A checkpointed Blob no longer matches the verified corpus.");
            }
        }

        foreach (var (name, content) in files)
        {
            if (state.Files.ContainsKey(name))
            {
                continue;
            }

            state.Pending = name;
            await SearchCheckpoint.SaveAsync(statePath, state, cancellationToken);
            string etag = await store.CreateAsync(name, content, cancellationToken);
            if (string.IsNullOrWhiteSpace(etag) || !await store.MatchesAsync(name, etag, content, cancellationToken))
            {
                throw new InvalidDataException("The uploaded Blob did not match the verified corpus.");
            }

            state.Files.Add(name, new BlobCheckpoint { ETag = etag, Sha256 = Convert.ToHexString(SHA256.HashData(content)) });
            state.Pending = null;
            await SearchCheckpoint.SaveAsync(statePath, state, cancellationToken);
        }

        await VerifyMembershipAsync(files.Keys, cancellationToken);

        async Task VerifyMembershipAsync(IEnumerable<string> expected, CancellationToken token)
        {
            var actual = await store.ListAsync(token);
            if (actual.Count != expected.Count() || !actual.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            {
                throw new InvalidDataException("The Search corpus container contains missing or untracked Blobs; do not adopt or overwrite them.");
            }
        }

    }

    private sealed class State
    {
        public string Kind { get; set; } = string.Empty;
        public string ManifestHash { get; set; } = string.Empty;
        public string Container { get; set; } = string.Empty;
        public string? Pending { get; set; }
        public Dictionary<string, BlobCheckpoint> Files { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class BlobCheckpoint
    {
        public string ETag { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }
}
