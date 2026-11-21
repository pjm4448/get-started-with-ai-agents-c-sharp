using System.ClientModel.Primitives;
using System.Text.Json;
using AiAgents.Setup;

namespace AiAgents.UnitTests;

public sealed class SearchIndexDefinitionTests
{
    [Fact]
    public void IndexWireDefinitionSupportsHybridQueriesWithoutModelSecrets()
    {
        var index = SearchIndexDefinition.Create("fictional-index", new Uri("https://example.openai.azure.com"),
            "embedding", "text-embedding-3-small", 1536);

        using var document = JsonDocument.Parse(ModelReaderWriter.Write(index, new ModelReaderWriterOptions("W")).ToString());
        var root = document.RootElement;
        var fields = root.GetProperty("fields").EnumerateArray().ToDictionary(field => field.GetProperty("name").GetString()!);
        Assert.True(fields["chunk_id"].GetProperty("key").GetBoolean());
        Assert.Equal("keyword", fields["chunk_id"].GetProperty("analyzer").GetString());
        Assert.True(fields["parent_id"].GetProperty("filterable").GetBoolean());
        Assert.True(fields["chunk"].GetProperty("searchable").GetBoolean());
        Assert.True(fields["chunk"].GetProperty("retrievable").GetBoolean());
        Assert.True(fields["source"].GetProperty("retrievable").GetBoolean());
        var vector = fields["chunk_vector"];
        Assert.Equal("Collection(Edm.Single)", vector.GetProperty("type").GetString());
        Assert.True(vector.GetProperty("searchable").GetBoolean());
        Assert.False(vector.GetProperty("retrievable").GetBoolean());
        Assert.Equal(1536, vector.GetProperty("dimensions").GetInt32());

        var search = root.GetProperty("vectorSearch");
        var profile = Assert.Single(search.GetProperty("profiles").EnumerateArray());
        var algorithm = Assert.Single(search.GetProperty("algorithms").EnumerateArray());
        var vectorizer = Assert.Single(search.GetProperty("vectorizers").EnumerateArray());
        Assert.Equal(vector.GetProperty("vectorSearchProfile").GetString(), profile.GetProperty("name").GetString());
        Assert.Equal(algorithm.GetProperty("name").GetString(), profile.GetProperty("algorithm").GetString());
        Assert.Equal(vectorizer.GetProperty("name").GetString(), profile.GetProperty("vectorizer").GetString());
        var parameters = vectorizer.GetProperty("azureOpenAIParameters");
        Assert.Equal("text-embedding-3-small", parameters.GetProperty("modelName").GetString());
        Assert.False(parameters.TryGetProperty("apiKey", out _));
        Assert.False(parameters.TryGetProperty("authIdentity", out _));
    }

    [Fact]
    public void SkillsetProjectsChunksWithTheSameKeylessEmbeddingConfiguration()
    {
        var endpoint = new Uri("https://example.openai.azure.com");
        var index = SearchIndexDefinition.Create("fictional-index", endpoint, "embedding", "text-embedding-3-small", 1536);
        var skillset = SearchIndexDefinition.CreateSkillset("fictional-index", endpoint, "embedding", "text-embedding-3-small", 1536);
        var wireOptions = new ModelReaderWriterOptions("W");
        using var indexDocument = JsonDocument.Parse(ModelReaderWriter.Write(index, wireOptions).ToString());
        using var skillDocument = JsonDocument.Parse(ModelReaderWriter.Write(skillset, wireOptions).ToString());
        var skills = skillDocument.RootElement.GetProperty("skills");
        var split = skills[0];
        var embedding = skills[1];
        Assert.Equal("/document/content", split.GetProperty("inputs")[0].GetProperty("source").GetString());
        Assert.Equal("/document/pages/*", embedding.GetProperty("context").GetString());
        Assert.Equal("chunk_vector", embedding.GetProperty("outputs")[0].GetProperty("targetName").GetString());
        var field = indexDocument.RootElement.GetProperty("fields").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "chunk_vector");
        Assert.Equal(field.GetProperty("dimensions").GetInt32(), embedding.GetProperty("dimensions").GetInt32());
        var parameters = indexDocument.RootElement.GetProperty("vectorSearch").GetProperty("vectorizers")[0].GetProperty("azureOpenAIParameters");
        Assert.Equal(parameters.GetProperty("modelName").GetString(), embedding.GetProperty("modelName").GetString());
        Assert.Equal(parameters.GetProperty("deploymentId").GetString(), embedding.GetProperty("deploymentId").GetString());
        Assert.Equal(parameters.GetProperty("resourceUri").GetString(), embedding.GetProperty("resourceUri").GetString());
        Assert.False(embedding.TryGetProperty("apiKey", out _));
        Assert.False(embedding.TryGetProperty("authIdentity", out _));

        var projection = skillDocument.RootElement.GetProperty("indexProjections");
        Assert.Equal("skipIndexingParentDocuments", projection.GetProperty("parameters").GetProperty("projectionMode").GetString());
        var selector = Assert.Single(projection.GetProperty("selectors").EnumerateArray());
        Assert.Equal(index.Name, selector.GetProperty("targetIndexName").GetString());
        Assert.Equal("parent_id", selector.GetProperty("parentKeyFieldName").GetString());
        var mappings = selector.GetProperty("mappings").EnumerateArray()
            .ToDictionary(item => item.GetProperty("name").GetString()!, item => item.GetProperty("source").GetString());
        Assert.Equal("/document/pages/*", mappings["chunk"]);
        Assert.Equal("/document/pages/*/chunk_vector", mappings["chunk_vector"]);
        Assert.Equal("/document/metadata_storage_name", mappings["title"]);
        Assert.Equal("/document/metadata_storage_path", mappings["source"]);
        Assert.False(mappings.ContainsKey("parent_id"));
    }

    [Theory]
    [InlineData("markdown", "markdown", ".md")]
    [InlineData("pdf", "default", ".pdf")]
    public void IndexersStartDisabledWithSeparateStrictParsing(string type, string parsingMode, string extension)
    {
        var indexer = SearchIndexDefinition.CreateIndexer("fictional-index", type);
        using var document = JsonDocument.Parse(
            ModelReaderWriter.Write(indexer, new ModelReaderWriterOptions("W")).ToString());
        var root = document.RootElement;
        Assert.True(root.GetProperty("disabled").GetBoolean());
        Assert.Equal($"fictional-index-{type}", root.GetProperty("dataSourceName").GetString());
        Assert.Equal("fictional-index", root.GetProperty("targetIndexName").GetString());
        Assert.Equal("fictional-index", root.GetProperty("skillsetName").GetString());
        var parameters = root.GetProperty("parameters");
        Assert.Equal(0, parameters.GetProperty("maxFailedItems").GetInt32());
        Assert.Equal(0, parameters.GetProperty("maxFailedItemsPerBatch").GetInt32());
        var configuration = parameters.GetProperty("configuration");
        Assert.Equal("private", configuration.GetProperty("executionEnvironment").GetString());
        Assert.Equal(parsingMode, configuration.GetProperty("parsingMode").GetString());
        Assert.Equal(extension, configuration.GetProperty("indexedFileNameExtensions").GetString());
        Assert.True(configuration.GetProperty("failOnUnprocessableDocument").GetBoolean());
        if (type == "markdown")
        {
            Assert.Equal("oneToMany", configuration.GetProperty("markdownParsingSubmode").GetString());
        }
        else
        {
            Assert.False(configuration.TryGetProperty("markdownParsingSubmode", out _));
        }
    }

    [Fact]
    public void IndexerRejectsUnknownFileTypes()
    {
        Assert.Throws<ArgumentException>(() => SearchIndexDefinition.CreateIndexer("fictional-index", "txt"));
    }

    [Theory]
    [InlineData("markdown")]
    [InlineData("pdf")]
    public void DataSourcesUseKeylessStorageIdentityAndDisjointPrefixes(string type)
    {
        const string resourceId = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/example/providers/Microsoft.Storage/storageAccounts/exampleaccount";
        var source = SearchIndexDefinition.CreateDataSource("fictional-index", type, resourceId, "fictional-corpus");
        var indexer = SearchIndexDefinition.CreateIndexer("fictional-index", type);
        using var document = JsonDocument.Parse(
            ModelReaderWriter.Write(source, new ModelReaderWriterOptions("W")).ToString());
        var root = document.RootElement;
        Assert.Equal(indexer.DataSourceName, root.GetProperty("name").GetString());
        Assert.Equal("azureblob", root.GetProperty("type").GetString());
        Assert.Equal($"ResourceId={resourceId};", root.GetProperty("credentials").GetProperty("connectionString").GetString());
        Assert.False(root.TryGetProperty("identity", out _));
        Assert.Equal($"{type}/", root.GetProperty("container").GetProperty("query").GetString());
    }

    [Theory]
    [InlineData("/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/example/providers/Microsoft.Storage/storageAccounts/example;AccountKey=secret")]
    [InlineData("/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/example/providers/Microsoft.Storage/storageAccounts/example/blobs/x")]
    [InlineData("/subscriptions/not-a-guid/resourceGroups/example/providers/Microsoft.Storage/storageAccounts/example")]
    public void RejectsInvalidOrSecretBearingStorageResourceIds(string resourceId)
    {
        Assert.Throws<ArgumentException>(() =>
            SearchIndexDefinition.CreateDataSource("fictional-index", "pdf", resourceId, "fictional-corpus"));
    }

    [Theory]
    [InlineData("text-embedding-ada-002", 1535)]
    [InlineData("text-embedding-3-small", 0)]
    [InlineData("text-embedding-3-small", 512)]
    [InlineData("text-embedding-3-small", 1537)]
    [InlineData("text-embedding-3-large", 3073)]
    [InlineData("unknown", 1536)]
    public void RejectsUnsupportedEmbeddingDimensions(string model, int dimensions)
    {
        Assert.Throws<ArgumentException>(() => SearchIndexDefinition.Create(
            "fictional-index", new Uri("https://example.openai.azure.com"), "embedding", model, dimensions));
    }

    [Theory]
    [InlineData("http://example.openai.azure.com")]
    [InlineData("https://example.openai.azure.com?api-key=secret")]
    [InlineData("https://user:secret@example.openai.azure.com")]
    [InlineData("https://example.openai.azure.com/embeddings")]
    [InlineData("https://example.openai.azure.com.evil.example")]
    public void RejectsUnsafeModelEndpoints(string endpoint)
    {
        Assert.Throws<ArgumentException>(() => SearchIndexDefinition.Create(
            "fictional-index", new Uri(endpoint), "embedding", "text-embedding-3-small", 1536));
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("two--hyphens")]
    [InlineData("-invalid")]
    public void RejectsInvalidIndexNames(string name)
    {
        Assert.Throws<ArgumentException>(() => SearchIndexDefinition.Create(
            name, new Uri("https://example.openai.azure.com"), "embedding", "text-embedding-3-small", 1536));
    }
}
