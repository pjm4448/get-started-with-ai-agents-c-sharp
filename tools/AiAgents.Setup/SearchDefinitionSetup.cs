using System.ClientModel.Primitives;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace AiAgents.Setup;

public abstract record SearchDefinition(string Key);
public sealed record SearchIndexResource(SearchIndex Value) : SearchDefinition($"index:{Value.Name}");
public sealed record SearchSkillsetResource(SearchIndexerSkillset Value) : SearchDefinition($"skillset:{Value.Name}");
public sealed record SearchDataSourceResource(SearchIndexerDataSourceConnection Value) : SearchDefinition($"datasource:{Value.Name}");
public sealed record SearchIndexerResource(SearchIndexer Value) : SearchDefinition($"indexer:{Value.Name}");
public sealed record SearchDefinitionSnapshot(string ETag, string Json);

public interface ISearchDefinitionStore
{
    string Identity { get; }
    Task<SearchDefinitionSnapshot?> GetAsync(SearchDefinition definition, CancellationToken cancellationToken);
    Task<string> CreateAsync(SearchDefinition definition, CancellationToken cancellationToken);
}

public sealed class SearchAzureDefinitionStore : ISearchDefinitionStore
{
    private readonly SearchIndexClient _indexes;
    private readonly SearchIndexerClient _indexers;

    public SearchAzureDefinitionStore(Uri endpoint, TokenCredential credential, HttpClient? http = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        const string suffix = ".search.windows.net";
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps || !endpoint.IsDefaultPort ||
            endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            endpoint.AbsolutePath != "/" || !endpoint.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
            endpoint.Host.Length == suffix.Length)
        {
            throw new ArgumentException("A credential-free public-cloud Search service HTTPS base URI is required.", nameof(endpoint));
        }

        SearchClientOptions options = new();
        options.Retry.MaxRetries = 0;
        if (http is not null)
        {
            options.Transport = new HttpClientTransport(http);
        }

        _indexes = new SearchIndexClient(endpoint, credential, options);
        _indexers = new SearchIndexerClient(endpoint, credential, options);
        Identity = endpoint.AbsoluteUri;
    }

    public string Identity { get; }

    public async Task<SearchDefinitionSnapshot?> GetAsync(SearchDefinition definition, CancellationToken cancellationToken)
    {
        try
        {
            return definition switch
            {
                SearchIndexResource item => Snapshot((await _indexes.GetIndexAsync(item.Value.Name, cancellationToken)).Value),
                SearchSkillsetResource item => Snapshot((await _indexers.GetSkillsetAsync(item.Value.Name, cancellationToken)).Value),
                SearchDataSourceResource item => Snapshot((await _indexers.GetDataSourceConnectionAsync(item.Value.Name, cancellationToken)).Value),
                SearchIndexerResource item => Snapshot((await _indexers.GetIndexerAsync(item.Value.Name, cancellationToken)).Value),
                _ => throw new ArgumentException("Unknown Search definition type.", nameof(definition))
            };
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task<string> CreateAsync(SearchDefinition definition, CancellationToken cancellationToken) =>
        definition switch
        {
            SearchIndexResource item => Snapshot((await _indexes.CreateIndexAsync(item.Value, cancellationToken)).Value).ETag,
            SearchSkillsetResource item => Snapshot((await _indexers.CreateSkillsetAsync(item.Value, cancellationToken)).Value).ETag,
            SearchDataSourceResource item => Snapshot((await _indexers.CreateDataSourceConnectionAsync(item.Value, cancellationToken)).Value).ETag,
            SearchIndexerResource item => Snapshot((await _indexers.CreateIndexerAsync(item.Value, cancellationToken)).Value).ETag,
            _ => throw new ArgumentException("Unknown Search definition type.", nameof(definition))
        };

    private static SearchDefinitionSnapshot Snapshot(SearchIndex value) => new(value.ETag?.ToString() ?? "", Wire(value));
    private static SearchDefinitionSnapshot Snapshot(SearchIndexerSkillset value) => new(value.ETag?.ToString() ?? "", Wire(value));
    private static SearchDefinitionSnapshot Snapshot(SearchIndexerDataSourceConnection value) => new(value.ETag?.ToString() ?? "", Wire(value));
    private static SearchDefinitionSnapshot Snapshot(SearchIndexer value) => new(value.ETag?.ToString() ?? "", Wire(value));

    internal static string Wire<T>(T value) => ModelReaderWriter.Write(value, new ModelReaderWriterOptions("W")).ToString();
}

public sealed class SearchDefinitionSetup(ISearchDefinitionStore store)
{
    public async Task CreateAsync(
        string indexName, Uri modelEndpoint, string deploymentName, string modelName, int dimensions,
        string storageResourceId, string containerName, string statePath, CancellationToken cancellationToken)
    {
        SearchDefinition[] definitions =
        [
            new SearchIndexResource(SearchIndexDefinition.Create(indexName, modelEndpoint, deploymentName, modelName, dimensions)),
            new SearchSkillsetResource(SearchIndexDefinition.CreateSkillset(indexName, modelEndpoint, deploymentName, modelName, dimensions)),
            new SearchDataSourceResource(SearchIndexDefinition.CreateDataSource(indexName, "markdown", storageResourceId, containerName)),
            new SearchDataSourceResource(SearchIndexDefinition.CreateDataSource(indexName, "pdf", storageResourceId, containerName)),
            new SearchIndexerResource(SearchIndexDefinition.CreateIndexer(indexName, "markdown")),
            new SearchIndexerResource(SearchIndexDefinition.CreateIndexer(indexName, "pdf"))
        ];
        var wire = definitions.ToDictionary(item => item.Key, Wire, StringComparer.Ordinal);
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(wire))));
        statePath = Path.GetFullPath(statePath);
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        await using var stateLock = new FileStream(
            statePath + ".lock", FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);

        State state;
        if (File.Exists(statePath))
        {
            await using var input = File.OpenRead(statePath);
            state = await JsonSerializer.DeserializeAsync<State>(input, ProductManifest.JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("The Search definition state is empty.");
            if (state.Kind != "search-definition-v1" || state.Service != store.Identity ||
                state.Fingerprint != fingerprint || state.Resources is null ||
                state.Resources.Any(pair => !wire.ContainsKey(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
            {
                throw new InvalidDataException("Search definition state does not match the requested service and definitions.");
            }

            if (state.Pending is not null)
            {
                throw new InvalidDataException("A previous Search definition write has an unknown outcome; reconcile it before proceeding.");
            }
        }
        else
        {
            state = new State { Kind = "search-definition-v1", Service = store.Identity, Fingerprint = fingerprint };
        }

        foreach (var definition in definitions)
        {
            var actual = await store.GetAsync(definition, cancellationToken);
            if (state.Resources.TryGetValue(definition.Key, out string? etag))
            {
                Verify(actual, etag, wire[definition.Key]);
            }
            else if (actual is not null)
            {
                throw new InvalidDataException("A requested Search resource already exists without a matching checkpoint; do not adopt it.");
            }
        }

        foreach (var definition in definitions)
        {
            if (state.Resources.ContainsKey(definition.Key))
            {
                continue;
            }

            state.Pending = definition.Key;
            await SearchCheckpoint.SaveAsync(statePath, state, cancellationToken);
            string etag = await store.CreateAsync(definition, cancellationToken);
            var actual = await store.GetAsync(definition, cancellationToken);
            Verify(actual, etag, wire[definition.Key]);
            state.Resources.Add(definition.Key, etag);
            state.Pending = null;
            await SearchCheckpoint.SaveAsync(statePath, state, cancellationToken);
        }
    }

    private static string Wire(SearchDefinition definition) => definition switch
    {
        SearchIndexResource item => SearchAzureDefinitionStore.Wire(item.Value),
        SearchSkillsetResource item => SearchAzureDefinitionStore.Wire(item.Value),
        SearchDataSourceResource item => SearchAzureDefinitionStore.Wire(item.Value),
        SearchIndexerResource item => SearchAzureDefinitionStore.Wire(item.Value),
        _ => throw new ArgumentException("Unknown Search definition type.", nameof(definition))
    };

    private static void Verify(SearchDefinitionSnapshot? actual, string etag, string expectedJson)
    {
        if (actual is null || string.IsNullOrWhiteSpace(etag) || actual.ETag != etag ||
            !JsonNode.DeepEquals(Normalize(actual.Json), Normalize(expectedJson)))
        {
            throw new InvalidDataException("The Search resource readback does not exactly match its definition and checkpoint.");
        }
    }

    private static JsonNode Normalize(string json)
    {
        var node = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidDataException("The Search definition readback is not a JSON object.");
        node.Remove("@odata.etag");
        return node;
    }

    private sealed class State
    {
        public string Kind { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public string? Pending { get; set; }
        public Dictionary<string, string> Resources { get; set; } = new(StringComparer.Ordinal);
    }
}
