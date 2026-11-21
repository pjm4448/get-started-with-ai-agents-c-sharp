using System.Security.Cryptography;
using System.Text.Json;
using AiAgents.Setup;
using Azure;
using Azure.Core;

namespace AiAgents.UnitTests;

public sealed class SearchBlobUploadTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"search-upload-{Guid.NewGuid():N}");
    private string StatePath => Path.Combine(_directory, "search-state.json");
    private string MarkdownManifest => Path.Combine(_directory, "markdown.json");
    private string PdfManifest => Path.Combine(_directory, "pdf.json");

    [Fact]
    public async Task ResumesOnlyOwnedBlobsAndVerifiesBytes()
    {
        await CreateCorpusAsync();
        var store = new FakeStore();
        var upload = new SearchBlobUpload(store);

        await upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default);
        Assert.Equal(2, store.Writes);
        Assert.Equal(["markdown/guide.md", "pdf/guide.pdf"], store.Blobs.Keys.Order(StringComparer.Ordinal));
        await upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default);
        Assert.Equal(2, store.Writes);

        store.Blobs["markdown/guide.md"] = ("etag", "changed"u8.ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default));
        Assert.Equal(2, store.Writes);
    }

    [Fact]
    public async Task RejectsUntrackedOrMissingBlobsWithoutWriting()
    {
        await CreateCorpusAsync();
        var store = new FakeStore();
        store.Blobs.Add("markdown/guide.md", ("existing", "original"u8.ToArray()));
        var upload = new SearchBlobUpload(store);
        await Assert.ThrowsAsync<InvalidDataException>(() => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default));
        Assert.Equal(0, store.Writes);

        store.Blobs.Clear();
        await upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default);
        store.Blobs.Remove("pdf/guide.pdf");
        await Assert.ThrowsAsync<InvalidDataException>(() => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default));
        Assert.Equal(2, store.Writes);
    }

    [Fact]
    public async Task UnknownWriteOutcomeBlocksAllFurtherRemoteCalls()
    {
        await CreateCorpusAsync();
        var store = new FakeStore { FailAfterWrite = true };
        var upload = new SearchBlobUpload(store);

        await Assert.ThrowsAsync<IOException>(() => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default));
        int calls = store.Calls;
        store.FailAfterWrite = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default));
        Assert.Equal(calls, store.Calls);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task CancellationAfterWriteLeavesPendingCheckpoint()
    {
        await CreateCorpusAsync();
        using var cancellation = new CancellationTokenSource();
        var store = new FakeStore { AfterWrite = cancellation.Cancel };
        var upload = new SearchBlobUpload(store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, cancellation.Token));
        int calls = store.Calls;
        await Assert.ThrowsAsync<InvalidDataException>(() => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default));
        Assert.Equal(calls, store.Calls);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task RejectsChangedCorpusOrDifferentContainerBeforeRemoteCalls()
    {
        await CreateCorpusAsync();
        var store = new FakeStore();
        var upload = new SearchBlobUpload(store);
        await upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default);
        int calls = store.Calls;
        store.Identity = "another-container";
        await Assert.ThrowsAsync<InvalidDataException>(() => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default));
        Assert.Equal(calls, store.Calls);
        store.Identity = "container";
        await File.WriteAllTextAsync(Path.Combine(_directory, "guide.md"), "changed");
        await Assert.ThrowsAsync<InvalidDataException>(() => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default));
        Assert.Equal(calls, store.Calls);
    }

    [Fact]
    public async Task RejectsPublicContainerAndFileSearchState()
    {
        await CreateCorpusAsync();
        var store = new FakeStore { IsPrivate = false };
        var upload = new SearchBlobUpload(store);
        await Assert.ThrowsAsync<InvalidDataException>(() => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default));
        Assert.Equal(0, store.Writes);

        string oldState = "{\"schemaVersion\":1,\"projectEndpoint\":\"https://example\"}";
        await File.WriteAllTextAsync(StatePath, oldState);
        store.IsPrivate = true;
        int calls = store.Calls;
        await Assert.ThrowsAnyAsync<JsonException>(() => upload.UploadAsync(MarkdownManifest, PdfManifest, StatePath, default));
        Assert.Equal(oldState, await File.ReadAllTextAsync(StatePath));
        Assert.Equal(calls, store.Calls);
    }

    [Fact]
    public async Task AzureUploadUsesConditionalCreateAndDoesNotRetryFailures()
    {
        using var handler = new UploadHandler();
        using var http = new HttpClient(handler);
        var store = new SearchAzureBlobStore(
            new Uri("https://example.blob.core.windows.net/corpus"), new TestCredential(), http);

        Assert.Equal("\"test-etag\"", await store.CreateAsync("markdown/guide.md", "data"u8.ToArray(), default));
        Assert.Equal("*", handler.IfNoneMatch);
        Assert.Equal("text/markdown", handler.ContentType);

        handler.Fail = true;
        await Assert.ThrowsAsync<RequestFailedException>(() => store.CreateAsync("pdf/guide.pdf", "data"u8.ToArray(), default));
        Assert.Equal(2, handler.Requests);
        Assert.Equal("application/pdf", handler.ContentType);
    }

    [Theory]
    [InlineData("https://example.blob.core.windows.net/corpus?sig=secret")]
    [InlineData("https://example.blob.core.windows.net/corpus/nested")]
    [InlineData("https://example.blob.core.windows.net:444/corpus")]
    [InlineData("https://other.example.com/corpus")]
    public void AzureStoreRejectsUnsafeTargets(string uri)
    {
        Assert.Throws<ArgumentException>(() => new SearchAzureBlobStore(new Uri(uri), new TestCredential()));
    }

    [Fact]
    public async Task AzureReadbackPinsTheEtagAndComparesExactBytes()
    {
        using var handler = new ReadHandler();
        using var http = new HttpClient(handler);
        var store = new SearchAzureBlobStore(
            new Uri("https://example.blob.core.windows.net/corpus"), new TestCredential(), http);

        Assert.True(await store.MatchesAsync("markdown/guide.md", "\"test-etag\"", "data"u8.ToArray(), default));
        Assert.Equal(2, handler.Requests);
        Assert.All(handler.Conditions, condition => Assert.Equal("\"test-etag\"", condition));
        Assert.False(await store.MatchesAsync("markdown/guide.md", "\"test-etag\"", "else"u8.ToArray(), default));
    }

    private sealed class ReadHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public List<string> Conditions { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Conditions.Add(request.Headers.IfMatch.ToString());
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("data"u8.ToArray())
            };
            response.Content.Headers.ContentLength = 4;
            response.Headers.TryAddWithoutValidation("ETag", "\"test-etag\"");
            response.Headers.TryAddWithoutValidation("x-ms-request-id", "test-request");
            response.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
            return Task.FromResult(response);
        }
    }

    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class UploadHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public bool Fail { get; set; }
        public string? IfNoneMatch { get; private set; }
        public string? ContentType { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            IfNoneMatch = request.Headers.IfNoneMatch.ToString();
            ContentType = request.Headers.TryGetValues("x-ms-blob-content-type", out var values)
                ? values.Single() : null;
            var response = new HttpResponseMessage(Fail ? System.Net.HttpStatusCode.InternalServerError : System.Net.HttpStatusCode.Created);
            response.Headers.TryAddWithoutValidation("ETag", "\"test-etag\"");
            response.Headers.TryAddWithoutValidation("x-ms-request-id", "test-request");
            response.Content = new StringContent(string.Empty);
            return Task.FromResult(response);
        }
    }

    private async Task CreateCorpusAsync()
    {
        Directory.CreateDirectory(_directory);
        await CreateManifestAsync("guide.md", "fictional markdown", MarkdownManifest);
        await CreateManifestAsync("guide.pdf", "fictional pdf", PdfManifest);
    }

    private async Task CreateManifestAsync(string name, string content, string manifest)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
        await File.WriteAllBytesAsync(Path.Combine(_directory, name), bytes);
        await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            files = new[] { new { name, sha256 = Convert.ToHexString(SHA256.HashData(bytes)) } }
        }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FakeStore : ISearchBlobStore
    {
        public string Identity { get; set; } = "container";
        public bool IsPrivate { get; set; } = true;
        public bool FailAfterWrite { get; set; }
        public Action? AfterWrite { get; set; }
        public int Writes { get; private set; }
        public int Calls { get; private set; }
        public Dictionary<string, (string ETag, byte[] Content)> Blobs { get; } = new(StringComparer.Ordinal);

        public Task CheckPrivateAsync(CancellationToken cancellationToken)
        {
            Calls++;
            if (!IsPrivate)
            {
                throw new InvalidDataException("Container is public.");
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<string>>(Blobs.Keys.ToArray());
        }

        public Task<string> CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
        {
            Calls++;
            Writes++;
            Blobs.Add(name, ("etag", content.ToArray()));
            AfterWrite?.Invoke();
            if (FailAfterWrite)
            {
                throw new IOException("Unknown write outcome.");
            }

            return Task.FromResult("etag");
        }

        public Task<bool> MatchesAsync(string name, string etag, byte[] content, CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Blobs.TryGetValue(name, out var blob) &&
                blob.ETag == etag && blob.Content.AsSpan().SequenceEqual(content));
        }
    }
}
