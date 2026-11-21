using System.ClientModel.Primitives;
using System.Text.Json.Nodes;
using AiAgents.Setup;
using Azure;
using Azure.Core;

namespace AiAgents.UnitTests;

public sealed class SearchDefinitionSetupTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"search-definitions-{Guid.NewGuid():N}");
    private string StatePath => Path.Combine(_directory, "definitions.json");
    private const string StorageId = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/example/providers/Microsoft.Storage/storageAccounts/exampleaccount";
    private static readonly Uri ModelEndpoint = new("https://example.openai.azure.com");

    [Fact]
    public async Task CreatesDisabledDefinitionsOnceAndChecksExactReadback()
    {
        var store = new FakeStore();
        await ApplyAsync(store);

        Assert.Equal(6, store.Writes);
        Assert.Equal(6, store.Resources.Count);
        Assert.All(store.Resources.Where(pair => pair.Key.StartsWith("indexer:", StringComparison.Ordinal)),
            pair => Assert.True(JsonNode.Parse(pair.Value.Json)!["disabled"]!.GetValue<bool>()));
        await ApplyAsync(store);
        Assert.Equal(6, store.Writes);

        var saved = store.Resources["index:fictional-index"];
        store.Resources["index:fictional-index"] = saved with { Json = "{}" };
        await Assert.ThrowsAsync<InvalidDataException>(() => ApplyAsync(store));
        Assert.Equal(6, store.Writes);

        store.Resources["index:fictional-index"] = saved with { ETag = "\"changed\"" };
        await Assert.ThrowsAsync<InvalidDataException>(() => ApplyAsync(store));
        Assert.Equal(6, store.Writes);
    }

    [Fact]
    public async Task RefusesUntrackedDefinitionsBeforeAnyWrites()
    {
        var store = new FakeStore();
        store.Resources.Add("indexer:fictional-index-pdf", new SearchDefinitionSnapshot("etag", "{}"));
        await Assert.ThrowsAsync<InvalidDataException>(() => ApplyAsync(store));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task UnknownWriteAndCancellationCannotBeRetried()
    {
        var store = new FakeStore { FailAfterWrite = true };
        await Assert.ThrowsAsync<IOException>(() => ApplyAsync(store));
        int calls = store.Calls;
        store.FailAfterWrite = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => ApplyAsync(store));
        Assert.Equal(calls, store.Calls);
        Assert.Equal(1, store.Writes);

        string canceledPath = Path.Combine(_directory, "canceled.json");
        using var cancellation = new CancellationTokenSource();
        var canceledStore = new FakeStore { AfterWrite = cancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ApplyAsync(canceledStore, canceledPath, cancellation.Token));
        calls = canceledStore.Calls;
        await Assert.ThrowsAsync<InvalidDataException>(() => ApplyAsync(canceledStore, canceledPath));
        Assert.Equal(calls, canceledStore.Calls);
    }

    [Fact]
    public async Task RejectsChangedDefinitionOrServiceBeforeRemoteAccess()
    {
        var store = new FakeStore();
        await ApplyAsync(store);
        int calls = store.Calls;
        await Assert.ThrowsAsync<InvalidDataException>(() => ApplyAsync(store, modelName: "text-embedding-ada-002"));
        store.Identity = "different-service";
        await Assert.ThrowsAsync<InvalidDataException>(() => ApplyAsync(store));
        Assert.Equal(calls, store.Calls);
    }

    [Theory]
    [InlineData("index", "/indexes")]
    [InlineData("skillset", "/skillsets")]
    [InlineData("datasource", "/datasources")]
    [InlineData("indexer", "/indexers")]
    public async Task AzureSdkCreatesWithoutOverwriteAndDoesNotRetryFailures(string kind, string path)
    {
        using var handler = new DefinitionHandler();
        using var http = new HttpClient(handler);
        var store = new SearchAzureDefinitionStore(new Uri("https://example.search.windows.net"), new TestCredential(), http);
        SearchDefinition definition = kind switch
        {
            "index" => new SearchIndexResource(SearchIndexDefinition.Create("fictional-index",
                ModelEndpoint, "embedding", "text-embedding-3-small", 1536)),
            "skillset" => new SearchSkillsetResource(SearchIndexDefinition.CreateSkillset("fictional-index",
                ModelEndpoint, "embedding", "text-embedding-3-small", 1536)),
            "datasource" => new SearchDataSourceResource(SearchIndexDefinition.CreateDataSource(
                "fictional-index", "markdown", StorageId, "fictional-corpus")),
            "indexer" => new SearchIndexerResource(SearchIndexDefinition.CreateIndexer("fictional-index", "markdown")),
            _ => throw new ArgumentException("Unexpected definition kind.", nameof(kind))
        };

        Assert.Null(await store.GetAsync(definition, default));
        Assert.Equal("\"test-etag\"", await store.CreateAsync(definition, default));
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(path, handler.LastPath);
        handler.FailCreate = true;
        await Assert.ThrowsAsync<RequestFailedException>(() => store.CreateAsync(definition, default));
        Assert.Equal(3, handler.Requests);
    }

    private Task ApplyAsync(FakeStore store, string? statePath = null, CancellationToken cancellationToken = default,
        string modelName = "text-embedding-3-small") =>
        new SearchDefinitionSetup(store).CreateAsync("fictional-index", ModelEndpoint, "embedding", modelName,
            1536, StorageId, "fictional-corpus", statePath ?? StatePath, cancellationToken);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FakeStore : ISearchDefinitionStore
    {
        public string Identity { get; set; } = "service";
        public bool FailAfterWrite { get; set; }
        public Action? AfterWrite { get; set; }
        public int Writes { get; private set; }
        public int Calls { get; private set; }
        public Dictionary<string, SearchDefinitionSnapshot> Resources { get; } = new(StringComparer.Ordinal);

        public Task<SearchDefinitionSnapshot?> GetAsync(SearchDefinition definition, CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Resources.GetValueOrDefault(definition.Key));
        }

        public Task<string> CreateAsync(SearchDefinition definition, CancellationToken cancellationToken)
        {
            Calls++;
            Writes++;
            string json = definition switch
            {
                SearchIndexResource item => Wire(item.Value),
                SearchSkillsetResource item => Wire(item.Value),
                SearchDataSourceResource item => Wire(item.Value),
                SearchIndexerResource item => Wire(item.Value),
                _ => throw new ArgumentException("Unexpected definition.", nameof(definition))
            };
            Resources.Add(definition.Key, new SearchDefinitionSnapshot("\"etag\"", json));
            AfterWrite?.Invoke();
            if (FailAfterWrite)
            {
                throw new IOException("Unknown write outcome.");
            }

            return Task.FromResult("\"etag\"");
        }

        private static string Wire<T>(T model) => ModelReaderWriter.Write(model, new ModelReaderWriterOptions("W")).ToString();
    }

    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class DefinitionHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public bool FailCreate { get; set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastPath { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastMethod = request.Method;
            LastPath = request.RequestUri!.AbsolutePath;
            string body = "{}";
            var status = System.Net.HttpStatusCode.NotFound;
            if (request.Method == HttpMethod.Post)
            {
                status = FailCreate ? System.Net.HttpStatusCode.InternalServerError : System.Net.HttpStatusCode.Created;
                var json = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
                json["@odata.etag"] = "\"test-etag\"";
                body = json.ToJsonString();
            }

            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("ETag", "\"test-etag\"");
            response.Headers.TryAddWithoutValidation("x-ms-request-id", "test-request");
            return response;
        }
    }
}
