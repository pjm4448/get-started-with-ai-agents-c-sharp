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
    private string ExpandedStatePath => Path.Combine(_directory, "expanded-state.json");
    private static VerifiedProducts Products => new(new string('A', 64), [new("guide.md", "Sample fact."u8.ToArray())]);
    private static VerifiedProducts ExpandedProducts => new(new string('B', 64),
        [new("guide.md", "Sample fact."u8.ToArray()), new("extra.pdf", "Another fact."u8.ToArray())]);
    private static VerifiedProducts ReplacementProducts => new(new string('C', 64),
        [new("fictional-1.md", "Imaginary exhibit one."u8.ToArray()), new("fictional-2.md", "Imaginary exhibit two."u8.ToArray())]);
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

    [Fact]
    public async Task CreatesAndReusesExpandedVersionWithoutChangingBaseline()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        Assert.Equal("1", await setup.ApplyAsync(Products, StatePath, default));
        string baseline = await File.ReadAllTextAsync(StatePath);

        Assert.Equal("2", await setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, default));
        int writes = handler.Writes;
        Assert.Equal("2", await setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, default));

        Assert.Equal(writes, handler.Writes);
        Assert.Equal(3, handler.Uploads);
        Assert.Equal(1, handler.AgentCreates);
        Assert.Equal(1, handler.AgentVersionCreates);
        Assert.Equal(baseline, await File.ReadAllTextAsync(StatePath));
        Assert.Equal("vs_1", handler.Definitions["1"].GetProperty("tools")[0].GetProperty("vector_store_ids")[0].GetString());
        Assert.Equal("vs_2", handler.Definitions["2"].GetProperty("tools")[0].GetProperty("vector_store_ids")[0].GetString());
    }

    [Fact]
    public async Task RefusesChangedBaselineOrStaleLatestBeforeCreatingResources()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        int writes = handler.Writes;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyVersionAsync(ExpandedProducts, Products with { ManifestHash = new string('C', 64) },
                StatePath, ExpandedStatePath, default));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyVersionAsync(new VerifiedProducts(new string('B', 64),
                [new("guide.md", "Changed fact."u8.ToArray())]), Products, StatePath, ExpandedStatePath, default));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyVersionAsync(Products with { ManifestHash = new string('B', 64) },
                Products, StatePath, ExpandedStatePath, default));
        handler.UnexpectedFile = true;
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, default));
        handler.UnexpectedFile = false;
        handler.LatestVersion = "another-version";
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, default));

        Assert.Equal(writes, handler.Writes);
        Assert.False(File.Exists(ExpandedStatePath));
    }

    [Fact]
    public async Task RechecksLatestAfterIngestionBeforeVersionWrite()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        int originalPolls = handler.Polls;
        handler.OnPoll = () =>
        {
            if (handler.Polls == originalPolls + 2)
            {
                handler.LatestVersion = "another-version";
            }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, default));

        Assert.Equal(0, handler.AgentVersionCreates);
        Assert.True(File.Exists(ExpandedStatePath));
    }

    [Fact]
    public async Task RejectsSameStateAndChangedBaselineDefinitionBeforeWrites()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        int requests = handler.Requests;
        int writes = handler.Writes;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, StatePath, default));
        Assert.Equal(requests, handler.Requests);
        handler.Definitions["1"] = JsonSerializer.Deserialize<JsonElement>(
            """{"kind":"prompt","model":"different-model","instructions":"changed","tools":[]}""");
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, default));

        Assert.Equal(writes, handler.Writes);
        Assert.False(File.Exists(ExpandedStatePath));
    }

    [Fact]
    public async Task ExpandedIngestionCancellationResumesWithoutDuplicateUploads()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        int originalPolls = handler.Polls;
        handler.OnPoll = () =>
        {
            if (handler.Polls == originalPolls + 2)
            {
                cancellation.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, cancellation.Token));
        Assert.Equal(0, handler.AgentVersionCreates);
        handler.OnPoll = null;
        Assert.Equal("2", await setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, default));
        Assert.Equal(3, handler.Uploads);
        Assert.Equal(1, handler.AgentVersionCreates);
    }

    [Fact]
    public async Task UncertainVersionCreationBlocksRetryWithoutAdditionalRequests()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        handler.FailVersion = true;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, default));
        int requests = handler.Requests;
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyVersionAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, default));

        Assert.Equal(requests, handler.Requests);
        Assert.Equal(1, handler.AgentVersionCreates);
        Assert.Contains("create retrieval agent version", await File.ReadAllTextAsync(ExpandedStatePath));
    }

    [Fact]
    public async Task ReplacementUploadsOnlyNewFilesAndPreservesBaselineOnRepeat()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        string baseline = await File.ReadAllTextAsync(StatePath);

        Assert.Equal("2", await setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, default));
        int writes = handler.Writes;
        Assert.Equal("2", await setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, default));

        Assert.Equal(writes, handler.Writes);
        Assert.Equal(3, handler.Uploads);
        Assert.DoesNotContain("guide.md", handler.UploadedBodies[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Sample fact.", handler.UploadedBodies[1], StringComparison.Ordinal);
        Assert.Contains("fictional-1.md", handler.UploadedBodies[1], StringComparison.Ordinal);
        Assert.Contains("fictional-2.md", handler.UploadedBodies[2], StringComparison.Ordinal);
        Assert.Equal(baseline, await File.ReadAllTextAsync(StatePath));
        Assert.Equal("vs_1", handler.Definitions["1"].GetProperty("tools")[0].GetProperty("vector_store_ids")[0].GetString());
        Assert.Equal("vs_2", handler.Definitions["2"].GetProperty("tools")[0].GetProperty("vector_store_ids")[0].GetString());
        Assert.Contains("fictional", handler.Definitions["2"].GetProperty("instructions").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReplacementRejectsBaselineFilesAndWrongStateModeBeforeRemoteWrites()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        int writes = handler.Writes;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyReplacementAsync(ExpandedProducts, Products, StatePath, ExpandedStatePath, default));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyReplacementAsync(new VerifiedProducts(new string('D', 64),
                [new("renamed.md", "Sample fact."u8.ToArray())]), Products, StatePath, ExpandedStatePath, default));
        Assert.Equal(writes, handler.Writes);
        Assert.False(File.Exists(ExpandedStatePath));

        await setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, default);
        string state = await File.ReadAllTextAsync(ExpandedStatePath);
        Assert.Contains("\"replacement\": true", state, StringComparison.Ordinal);
        await File.WriteAllTextAsync(ExpandedStatePath, state.Replace(
            "\"replacement\": true", "\"replacement\": false", StringComparison.Ordinal));
        int requests = handler.Requests;
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, default));
        Assert.Equal(requests, handler.Requests);
    }

    [Fact]
    public async Task ReplacementRetriesAfterIndexFailureWithoutDuplicatingUploads()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        int originalPolls = handler.Polls;
        handler.OnPoll = () =>
        {
            if (handler.Polls == originalPolls + 2)
            {
                handler.IndexingStatus = "failed";
            }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, default));
        Assert.Equal(0, handler.AgentVersionCreates);
        handler.OnPoll = null;
        handler.IndexingStatus = "completed";
        Assert.Equal("2", await setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, default));
        Assert.Equal(3, handler.Uploads);
    }

    [Fact]
    public async Task ReplacementCancellationResumesWithoutDuplicatingUploads()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        int originalPolls = handler.Polls;
        handler.OnPoll = () =>
        {
            if (handler.Polls == originalPolls + 2)
            {
                cancellation.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, cancellation.Token));
        Assert.Equal(0, handler.AgentVersionCreates);
        handler.OnPoll = null;
        Assert.Equal("2", await setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, default));
        Assert.Equal(3, handler.Uploads);
    }

    [Fact]
    public async Task ReplacementStopsIfLatestChangesDuringIngestion()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        int originalPolls = handler.Polls;
        handler.OnPoll = () =>
        {
            if (handler.Polls == originalPolls + 2)
            {
                handler.LatestVersion = "another-version";
            }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, default));
        Assert.Equal(0, handler.AgentVersionCreates);
        Assert.True(File.Exists(ExpandedStatePath));
    }

    [Fact]
    public async Task ReplacementUnknownVersionWriteBlocksRetry()
    {
        using var handler = new SetupHandler();
        using var http = new HttpClient(handler);
        var setup = new FileSearchSetup(Options, new TestCredential(), http);
        await setup.ApplyAsync(Products, StatePath, default);
        handler.FailVersion = true;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, default));
        int requests = handler.Requests;
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.ApplyReplacementAsync(ReplacementProducts, Products, StatePath, ExpandedStatePath, default));
        Assert.Equal(requests, handler.Requests);
        Assert.Equal(1, handler.AgentVersionCreates);
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
        public int AgentVersionCreates { get; private set; }
        public int Polls { get; private set; }
        public string IndexingStatus { get; set; } = "completed";
        public bool FailUpload { get; init; }
        public bool AgentExists { get; set; }
        public bool UnexpectedFile { get; set; }
        public bool FailVersion { get; set; }
        public string LatestVersion { get; set; } = "1";
        public Action? OnPoll { get; set; }
        public JsonElement Definition { get; private set; }
        public Dictionary<string, JsonElement> Definitions { get; } = [];
        public List<string> UploadedBodies { get; } = [];
        private readonly Dictionary<string, string> _storeNames = [];
        private readonly Dictionary<string, List<string>> _storeFiles = [];

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
                    string storeId = $"vs_{_storeNames.Count + 1}";
                    _storeNames.Add(storeId, body.RootElement.GetProperty("name").GetString()!);
                    _storeFiles.Add(storeId, []);
                    return Json(Store(storeId));
                }

                if (path.Contains("/vector_stores/", StringComparison.Ordinal) && path.EndsWith("/files", StringComparison.Ordinal))
                {
                    string storeId = path.Split('/')[^2];
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                    string fileId = body.RootElement.GetProperty("file_id").GetString()!;
                    _storeFiles[storeId].Add(fileId);
                    return Json(VectorFile(storeId, fileId));
                }

                if (path.EndsWith("/files", StringComparison.Ordinal))
                {
                    Uploads++;
                    UploadedBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                    return FailUpload ? Json("""{"error":{"message":"private provider failure"}}""", HttpStatusCode.ServiceUnavailable) :
                        Json($$"""{"id":"file_{{Uploads}}","object":"file","bytes":12,"created_at":1,"filename":"guide.md","purpose":"assistants","status":"processed"}""");
                }

                if (path.EndsWith("/agents/aiagents-products/versions", StringComparison.Ordinal))
                {
                    AgentVersionCreates++;
                    if (FailVersion)
                    {
                        return Json("""{"error":{"message":"private provider failure"}}""", HttpStatusCode.ServiceUnavailable);
                    }

                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                    Definition = body.RootElement.GetProperty("definition").Clone();
                    LatestVersion = "2";
                    Definitions.Add(LatestVersion, Definition);
                    return Json("""{"object":"agent.version","name":"aiagents-products","version":"2"}""");
                }

                if (path.EndsWith("/agents", StringComparison.Ordinal))
                {
                    AgentCreates++;
                    AgentExists = true;
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                    Definition = body.RootElement.GetProperty("definition").Clone();
                    Definitions.Add("1", Definition);
                    return Json("""{"name":"aiagents-products","versions":{"latest":{"version":"1"}}}""");
                }
            }
            else if (request.Method == HttpMethod.Get)
            {
                if (path.EndsWith("/agents/aiagents-products", StringComparison.Ordinal))
                {
                    return Json(JsonSerializer.Serialize(new { versions = new { latest = new { version = LatestVersion } } }),
                        AgentExists ? HttpStatusCode.OK : HttpStatusCode.NotFound);
                }

                if (path.Contains("/agents/aiagents-products/versions/", StringComparison.Ordinal))
                {
                    string version = path.Split('/')[^1];
                    return Json(JsonSerializer.Serialize(new { name = "aiagents-products", version, definition = Definitions[version] }));
                }

                if (path.Contains("/vector_stores/", StringComparison.Ordinal) && path.Contains("/files/", StringComparison.Ordinal))
                {
                    string[] parts = path.Split('/');
                    string storeId = parts[^3];
                    string fileId = parts[^1];
                    Polls++;
                    OnPoll?.Invoke();
                    cancellationToken.ThrowIfCancellationRequested();
                    return Json(VectorFile(storeId, fileId));
                }

                if (path.Contains("/vector_stores/", StringComparison.Ordinal) && path.EndsWith("/files", StringComparison.Ordinal))
                {
                    string storeId = path.Split('/')[^2];
                    var files = _storeFiles[storeId].Select(file => VectorFile(storeId, file)).ToList();
                    if (UnexpectedFile)
                    {
                        files.Add(VectorFile(storeId, "unexpected"));
                    }

                    return Json($$"""{"object":"list","data":[{{string.Join(",", files)}}],"first_id":"file_1","last_id":"file_1","has_more":false}""");
                }

                if (path.Contains("/vector_stores/", StringComparison.Ordinal))
                {
                    return Json(Store(path.Split('/')[^1]));
                }
            }

            throw new InvalidOperationException($"Unexpected offline request: {request.Method} {path}");
        }

        private string Store(string storeId) => JsonSerializer.Serialize(new
        {
            id = storeId,
            @object = "vector_store",
            created_at = 1,
            name = _storeNames[storeId],
            status = "completed",
            file_counts = new { in_progress = 0, completed = 1, failed = 0, cancelled = 0, total = 1 }
        });

        private string VectorFile(string storeId, string id) => JsonSerializer.Serialize(new
        {
            id,
            @object = "vector_store.file",
            created_at = 1,
            vector_store_id = storeId,
            status = IndexingStatus,
            last_error = (object?)null
        });

        private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
