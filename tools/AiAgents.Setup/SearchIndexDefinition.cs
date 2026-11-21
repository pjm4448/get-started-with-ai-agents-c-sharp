using Azure.Search.Documents.Indexes.Models;

namespace AiAgents.Setup;

public static class SearchIndexDefinition
{
    public static SearchIndex Create(
        string indexName, Uri modelEndpoint, string deploymentName, string modelName, int dimensions)
    {
        ValidateName(indexName, 119);

        ArgumentNullException.ThrowIfNull(modelEndpoint);
        if (!modelEndpoint.IsAbsoluteUri || modelEndpoint.Scheme != Uri.UriSchemeHttps ||
            !modelEndpoint.IsDefaultPort || modelEndpoint.UserInfo.Length != 0 ||
            modelEndpoint.Query.Length != 0 || modelEndpoint.Fragment.Length != 0 ||
            modelEndpoint.AbsolutePath != "/" ||
            !new[] { ".openai.azure.com", ".services.ai.azure.com", ".cognitiveservices.azure.com" }
                .Any(suffix => modelEndpoint.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                    modelEndpoint.Host.Length > suffix.Length))
        {
            throw new ArgumentException("The embedding endpoint must be a public-cloud Azure AI resource HTTPS base URI.", nameof(modelEndpoint));
        }

        if (string.IsNullOrWhiteSpace(deploymentName) || deploymentName.Length > 128 ||
            !deploymentName.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            throw new ArgumentException("The embedding deployment name is invalid.", nameof(deploymentName));
        }

        bool validDimensions = modelName switch
        {
            "text-embedding-ada-002" => dimensions == 1536,
            "text-embedding-3-small" => dimensions == 1536,
            "text-embedding-3-large" => dimensions == 3072,
            _ => false
        };
        if (!validDimensions)
        {
            throw new ArgumentException("The embedding model and vector dimensions must be a supported combination.", nameof(dimensions));
        }

        SearchIndex index = new(indexName)
        {
            Fields =
            {
                new SearchField("chunk_id", SearchFieldDataType.String)
                {
                    IsKey = true, IsSearchable = true, IsFilterable = true, AnalyzerName = LexicalAnalyzerName.Keyword
                },
                new SearchField("parent_id", SearchFieldDataType.String) { IsFilterable = true },
                new SearchField("title", SearchFieldDataType.String) { IsSearchable = true, IsHidden = false },
                new SearchField("source", SearchFieldDataType.String) { IsHidden = false },
                new SearchField("chunk", SearchFieldDataType.String) { IsSearchable = true, IsHidden = false },
                new SearchField("chunk_vector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true, IsHidden = true, VectorSearchDimensions = dimensions,
                    VectorSearchProfileName = "embedding-profile"
                }
            },
            VectorSearch = new VectorSearch
            {
                Algorithms = { new HnswAlgorithmConfiguration("hnsw") },
                Profiles = { new VectorSearchProfile("embedding-profile", "hnsw") { VectorizerName = "embedding" } },
                Vectorizers =
                {
                    new AzureOpenAIVectorizer("embedding")
                    {
                        Parameters = new AzureOpenAIVectorizerParameters
                        {
                            ResourceUri = modelEndpoint,
                            DeploymentName = deploymentName,
                            ModelName = modelName
                        }
                    }
                }
            }
        };
        return index;
    }

    public static SearchIndexerSkillset CreateSkillset(
        string indexName, Uri modelEndpoint, string deploymentName, string modelName, int dimensions)
    {
        _ = Create(indexName, modelEndpoint, deploymentName, modelName, dimensions);
        SplitSkill split = new(
            [new InputFieldMappingEntry("text") { Source = "/document/content" }],
            [new OutputFieldMappingEntry("textItems") { TargetName = "pages" }])
        {
            Name = "split",
            Context = "/document",
            TextSplitMode = TextSplitMode.Pages,
            MaximumPageLength = 2000,
            PageOverlapLength = 200
        };
        AzureOpenAIEmbeddingSkill embedding = new(
            [new InputFieldMappingEntry("text") { Source = "/document/pages/*" }],
            [new OutputFieldMappingEntry("embedding") { TargetName = "chunk_vector" }])
        {
            Name = "embedding",
            Context = "/document/pages/*",
            ResourceUri = modelEndpoint,
            DeploymentName = deploymentName,
            ModelName = modelName,
            Dimensions = dimensions
        };
        SearchIndexerIndexProjectionSelector selector = new(indexName, "parent_id", "/document/pages/*",
        [
            new InputFieldMappingEntry("chunk") { Source = "/document/pages/*" },
            new InputFieldMappingEntry("chunk_vector") { Source = "/document/pages/*/chunk_vector" },
            new InputFieldMappingEntry("title") { Source = "/document/metadata_storage_name" },
            new InputFieldMappingEntry("source") { Source = "/document/metadata_storage_path" }
        ]);
        return new SearchIndexerSkillset(indexName, [split, embedding])
        {
            IndexProjection = new SearchIndexerIndexProjection([selector])
            {
                Parameters = new SearchIndexerIndexProjectionsParameters
                {
                    ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
                }
            }
        };
    }

    public static SearchIndexer CreateIndexer(string indexName, string fileType)
    {
        ValidateName(indexName, 119);
        ValidateFileType(fileType);

        string name = $"{indexName}-{fileType}";
        IndexingParametersConfiguration configuration = new()
        {
            ExecutionEnvironment = IndexerExecutionEnvironment.Private,
            ParsingMode = fileType == "markdown" ? BlobIndexerParsingMode.Markdown : BlobIndexerParsingMode.Default,
            IndexedFileNameExtensions = fileType == "markdown" ? ".md" : ".pdf",
            FailOnUnsupportedContentType = true,
            FailOnUnprocessableDocument = true
        };
        if (fileType == "markdown")
        {
            configuration.MarkdownParsingSubmode = MarkdownParsingSubmode.OneToMany;
        }

        return new SearchIndexer(name, name, indexName)
        {
            SkillsetName = indexName,
            IsDisabled = true,
            Parameters = new IndexingParameters
            {
                MaxFailedItems = 0,
                MaxFailedItemsPerBatch = 0,
                IndexingParametersConfiguration = configuration
            }
        };
    }

    public static SearchIndexerDataSourceConnection CreateDataSource(
        string indexName, string fileType, string storageResourceId, string containerName)
    {
        ValidateName(indexName, 119);
        ValidateFileType(fileType);
        string[] parts = storageResourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!storageResourceId.StartsWith('/') || storageResourceId.EndsWith('/') ||
            parts.Length != 8 || !parts[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(parts[1], "D", out _) ||
            !parts[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase) ||
            parts[3].Length is < 1 or > 90 || parts[3].EndsWith('.') ||
            !parts[3].All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or '(' or ')') ||
            !parts[4].Equals("providers", StringComparison.OrdinalIgnoreCase) ||
            !parts[5].Equals("Microsoft.Storage", StringComparison.OrdinalIgnoreCase) ||
            !parts[6].Equals("storageAccounts", StringComparison.OrdinalIgnoreCase) ||
            parts[7].Length is < 3 or > 24 ||
            !parts[7].All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character)) ||
            storageResourceId.Contains("//", StringComparison.Ordinal))
        {
            throw new ArgumentException("The storage resource ID must identify a single Azure Storage account.", nameof(storageResourceId));
        }

        if (string.IsNullOrEmpty(containerName) || containerName.Length is < 3 or > 63 ||
            !char.IsAsciiLetterLower(containerName[0]) && !char.IsAsciiDigit(containerName[0]) ||
            !char.IsAsciiLetterLower(containerName[^1]) && !char.IsAsciiDigit(containerName[^1]) ||
            !containerName.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-') ||
            containerName.Contains("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("The Blob container name is invalid.", nameof(containerName));
        }

        return new SearchIndexerDataSourceConnection($"{indexName}-{fileType}", SearchIndexerDataSourceType.AzureBlob,
            $"ResourceId={storageResourceId};", new SearchIndexerDataContainer(containerName) { Query = $"{fileType}/" });
    }

    private static void ValidateFileType(string fileType)
    {
        if (fileType is not ("markdown" or "pdf"))
        {
            throw new ArgumentException("The indexer file type must be markdown or pdf.", nameof(fileType));
        }
    }

    private static void ValidateName(string name, int maximumLength)
    {
        if (string.IsNullOrEmpty(name) || name.Length > maximumLength ||
            !char.IsAsciiLetterLower(name[0]) ||
            !name.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-') ||
            name.EndsWith('-') || name.Contains("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("The Search name must use lowercase letters, digits and single hyphens.", nameof(name));
        }
    }
}
