using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiAgents.Setup;
using Azure.Core;

namespace AiAgents.UnitTests;

public sealed class FileSearchSetupTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"aiagents-setup-{Guid.NewGuid():N}");
    private string StatePath => Path.Combine(_directory, "state.json");
    private static VerifiedProducts Products => new(new string('A', 64), [new("guide.md", "Sample fact."u8.ToArray())]);
    private static FileSearchSetupOptions Options => new()
    {
        ProjectEndpoint = new Uri("https://test.services.ai.azure.com/api/projects/test"),
        ModelDeployment = "test-model"
    };

    [Fact]
    public async Task ValidatesExactManifestBytesAndRejectsChangedContent()
    {
        Directory.CreateDirectory(_directory);
        byte[] content = "A reviewed sample."u8.ToArray();
        await File.WriteAllBytesAsync(Path.Combine(_directory, "guide.md"), content);
        string manifest = Path.Combine(_directory, "manifest.json");
        await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            files = new[] { new { name = "guide.md", sha256 = Convert.ToHexString(SHA256.HashData(content)) } }
        }));

        var products = await ProductManifest.LoadAsync(manifest, default);
        Assert.Equal(content, Assert.Single(products.Files).Content);
        string reformatted = (await File.ReadAllTextAsync(manifest)).Replace(",", ",\r\n", StringComparison.Ordinal);
        await File.WriteAllTextAsync(manifest, reformatted);
        Assert.Equal(products.ManifestHash, (await ProductManifest.LoadAsync(manifest, default)).ManifestHash);
        await File.WriteAllTextAsync(Path.Combine(_directory, "guide.md"), "Unreviewed change.");

        await Assert.ThrowsAsync<InvalidDataException>(() => ProductManifest.LoadAsync(manifest, default));
    }

    [Theory]
    [InlineData("../private.md")]
    [InlineData("folder\\private.md")]
    public async Task RejectsManifestPathsOutsideTheApprovedDirectory(string name)
    {
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "manifest.json");
        await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            files = new[] { new { name, sha256 = new string('A', 64) } }
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() => ProductManifest.LoadAsync(manifest, default));
    }

    [Fact]
    public async Task RepeatedSetupVerifiesReadinessAndDoesNotRepeatWrites()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);

        Assert.Equal("1", await setup.ApplyAsync(Products, StatePath, default));
        int writes = handler.Writes;
        int polls = handler.Polls;
        Assert.Equal("1", await setup.ApplyAsync(Products, StatePath, default));

        Assert.Equal(writes, handler.Writes);
        Assert.True(handler.Polls > polls);
        Assert.Equal(1, handler.Uploads);
        Assert.Equal(1, handler.AgentCreates);
        Assert.Contains("file_search", handler.Definition.GetRawText());
        Assert.DoesNotContain("function", handler.Definition.GetRawText());
        Assert.False(File.Exists(StatePath + ".lock"));
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("cancelled")]
    public async Task FailedIndexingCannotCreateAnAgentOrReportSuccess(string status)
    {
        using var handler = new SetupHandler { IndexingStatus = status };
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);

        await Assert.ThrowsAsync<InvalidDataException>(() => setup.ApplyAsync(Products, StatePath, default));

        Assert.Equal(0, handler.AgentCreates);
        Assert.Equal(1, handler.Uploads);
        Assert.True(File.Exists(StatePath));
    }

    [Fact]
    public async Task IndexingTimeoutRetainsProgressAndResumesWithoutReuploading()
    {
        using var handler = new SetupHandler { IndexingStatus = "in_progress" };
        using var http = new HttpClient(handler);
        var options = Options;
        options.IngestionTimeout = TimeSpan.FromMilliseconds(50);
        var setup = new FileSearchSetup(options, new TestCredential(), http);

        await Assert.ThrowsAsync<TimeoutException>(() => setup.ApplyAsync(Products, StatePath, default));
        Assert.Equal(0, handler.AgentCreates);
        handler.IndexingStatus = "completed";
        Assert.Equal("1", await setup.ApplyAsync(Products, StatePath, default));
        Assert.Equal(1, handler.Uploads);
    }

    [Fact]
    public async Task CancellationDuringIndexingDoesNotRetryOrLoseTheCheckpoint()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new SetupHandler { OnPoll = cancellation.Cancel };
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => setup.ApplyAsync(Products, StatePath, cancellation.Token));

        Assert.Equal(1, handler.Polls);
        Assert.Equal(1, handler.Uploads);
        Assert.Equal(0, handler.AgentCreates);
        handler.OnPoll = null;
        Assert.Equal("1", await setup.ApplyAsync(Products, StatePath, default));
        Assert.Equal(1, handler.Uploads);
    }

    [Fact]
    public async Task UncertainWriteStopsFutureRunsInsteadOfDuplicatingUploads()
    {
        using var handler = new SetupHandler { FailUpload = true };
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);

        await Assert.ThrowsAsync<System.ClientModel.ClientResultException>(
            () => setup.ApplyAsync(Products, StatePath, default));
        int requests = handler.Requests;
        await Assert.ThrowsAsync<InvalidDataException>(() => setup.ApplyAsync(Products, StatePath, default));

        Assert.Equal(requests, handler.Requests);
        Assert.Equal(1, handler.Uploads);
        Assert.Equal(0, handler.AgentCreates);
    }

    [Fact]
    public async Task RefusesChangedManifestsAndUnexpectedRemoteMembership()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        int requests = handler.Requests;

        await Assert.ThrowsAsync<InvalidDataException>(() => setup.ApplyAsync(
            Products with { ManifestHash = new string('B', 64) }, StatePath, default));
        Assert.Equal(requests, handler.Requests);
        handler.UnexpectedFile = true;
        int writes = handler.Writes;
        await Assert.ThrowsAsync<InvalidDataException>(() => setup.ApplyAsync(Products, StatePath, default));
        Assert.Equal(writes, handler.Writes);
    }

    [Fact]
    public async Task NeverAdoptsOrOverwritesAnExistingUntrackedAgent()
    {
        using var handler = new SetupHandler { AgentExists = true };
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);

        await Assert.ThrowsAsync<InvalidDataException>(() => setup.ApplyAsync(Products, StatePath, default));

        Assert.Equal(0, handler.Writes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("offline-test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class SetupHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public int Writes { get; private set; }
        public int Uploads { get; private set; }
        public int AgentCreates { get; private set; }
        public int Polls { get; private set; }
        public string IndexingStatus { get; set; } = "completed";
        public bool FailUpload { get; init; }
        public bool AgentExists { get; set; }
        public bool UnexpectedFile { get; set; }
        public Action? OnPoll { get; set; }
        public JsonElement Definition { get; private set; }
        private string StoreName { get; set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post)
            {
                Writes++;
                if (path.EndsWith("/vector_stores", StringComparison.Ordinal))
                {
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                    StoreName = body.RootElement.GetProperty("name").GetString()!;
                    return Json(Store());
                }

                if (path.EndsWith("/vector_stores/vs_1/files", StringComparison.Ordinal))
                {
                    return Json(VectorFile());
                }

                if (path.EndsWith("/files", StringComparison.Ordinal))
                {
                    Uploads++;
                    return FailUpload ? Json("""{"error":{"message":"private provider failure"}}""", HttpStatusCode.ServiceUnavailable) :
                        Json("""{"id":"file_1","object":"file","bytes":12,"created_at":1,"filename":"guide.md","purpose":"assistants","status":"processed"}""");
                }

                if (path.EndsWith("/agents", StringComparison.Ordinal))
                {
                    AgentCreates++;
                    AgentExists = true;
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                    Definition = body.RootElement.GetProperty("definition").Clone();
                    return Json("""{"name":"aiagents-products","versions":{"latest":{"version":"1"}}}""");
                }
            }
            else if (request.Method == HttpMethod.Get)
            {
                if (path.EndsWith("/agents/aiagents-products", StringComparison.Ordinal))
                {
                    return Json("{}", AgentExists ? HttpStatusCode.OK : HttpStatusCode.NotFound);
                }

                if (path.EndsWith("/agents/aiagents-products/versions/1", StringComparison.Ordinal))
                {
                    return Json(JsonSerializer.Serialize(new { name = "aiagents-products", version = "1", definition = Definition }));
                }

                if (path.EndsWith("/vector_stores/vs_1", StringComparison.Ordinal))
                {
                    return Json(Store());
                }

                if (path.EndsWith("/files/file_1", StringComparison.Ordinal))
                {
                    Polls++;
                    OnPoll?.Invoke();
                    cancellationToken.ThrowIfCancellationRequested();
                    return Json(VectorFile());
                }

                if (path.EndsWith("/vector_stores/vs_1/files", StringComparison.Ordinal))
                {
                    string data = UnexpectedFile ? $"{VectorFile()},{VectorFile("unexpected")}" : VectorFile();
                    return Json($$"""{"object":"list","data":[{{data}}],"first_id":"file_1","last_id":"file_1","has_more":false}""");
                }
            }

            throw new InvalidOperationException($"Unexpected offline request: {request.Method} {path}");
        }

        private string Store() => JsonSerializer.Serialize(new
        {
            id = "vs_1",
            @object = "vector_store",
            created_at = 1,
            name = StoreName,
            status = "completed",
            file_counts = new { in_progress = 0, completed = 1, failed = 0, cancelled = 0, total = 1 }
        });

        private string VectorFile(string id = "file_1") => JsonSerializer.Serialize(new
        {
            id,
            @object = "vector_store.file",
            created_at = 1,
            vector_store_id = "vs_1",
            status = IndexingStatus,
            last_error = (object?)null
        });

        private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
