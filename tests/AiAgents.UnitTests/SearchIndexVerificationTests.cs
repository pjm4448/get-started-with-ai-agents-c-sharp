using System.Runtime.CompilerServices;
using AiAgents.Setup;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace AiAgents.UnitTests;

public sealed class SearchIndexVerificationTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly Uri Container = new("https://example.blob.core.windows.net/corpus");
    private static readonly VerifiedProducts Products = new(new string('A', 64),
        [new("guide.md", "markdown"u8.ToArray()), new("exhibit.pdf", "pdf"u8.ToArray())]);
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Markers =
        new Dictionary<string, IReadOnlyList<string>> { ["guide.md"] = ["Fictional markdown"], ["exhibit.pdf"] = ["PF-21", "teal"] };
    private static SearchChunk Markdown => new("chunk-1", "parent-1", "guide.md",
        "https://example.blob.core.windows.net/corpus/markdown/guide.md", "Fictional markdown.");
    private static SearchChunk Pdf => new("chunk-2", "parent-2", "exhibit.pdf",
        "https://example.blob.core.windows.net/corpus/pdf/exhibit.pdf", "The PF-21 exhibit is teal.");

    [Fact]
    public void RequiresFreshSuccessfulIndexerExecution()
    {
        SearchIndexVerification.VerifyIndexer(Status(), Start.AddMinutes(-1));
        Assert.Throws<InvalidDataException>(() => SearchIndexVerification.VerifyIndexer(Status(), Start));
        Assert.Throws<InvalidDataException>(() => SearchIndexVerification.VerifyIndexer(Status(
            execution: IndexerExecutionStatus.InProgress), null));
        Assert.Throws<InvalidDataException>(() => SearchIndexVerification.VerifyIndexer(Status(failed: 1), null));
        Assert.Throws<InvalidDataException>(() => SearchIndexVerification.VerifyIndexer(Status(items: 0), null));
        Assert.Throws<InvalidDataException>(() => SearchIndexVerification.VerifyIndexer(Status(warning: true), null));
        Assert.Throws<InvalidDataException>(() => SearchIndexVerification.VerifyIndexer(Status(
            overall: IndexerStatus.Error), null));
    }

    [Fact]
    public async Task AcceptsOnlyExpectedSourcesAndRequiredPdfText()
    {
        await SearchIndexVerification.VerifyChunksAsync(Chunks(Markdown, Pdf), Container, Products, Markers, default);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SearchIndexVerification.VerifyChunksAsync(Chunks(Markdown), Container, Products, Markers, default));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SearchIndexVerification.VerifyChunksAsync(Chunks(Markdown, Pdf with { Text = "Other text." }), Container, Products, Markers, default));
    }

    [Fact]
    public async Task RequiresVerificationTextForEverySource()
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> incomplete =
            new Dictionary<string, IReadOnlyList<string>> { ["exhibit.pdf"] = ["PF-21"] };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            SearchIndexVerification.VerifyChunksAsync(Chunks(Markdown, Pdf), Container, Products, incomplete, default));
    }

    [Theory]
    [InlineData("https://example.blob.core.windows.net/corpus/pdf/exhibit.pdf?sig=secret")]
    [InlineData("https://example.blob.core.windows.net/corpus/pdf/other.pdf")]
    [InlineData("https://other.blob.core.windows.net/corpus/pdf/exhibit.pdf")]
    public async Task RejectsUnexpectedSourceUris(string source)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SearchIndexVerification.VerifyChunksAsync(Chunks(Markdown, Pdf with { Source = source }), Container, Products, Markers, default));
    }

    [Fact]
    public async Task RejectsDuplicateKeysAndIncompleteChunks()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SearchIndexVerification.VerifyChunksAsync(Chunks(Markdown, Pdf with { Id = Markdown.Id }), Container, Products, Markers, default));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SearchIndexVerification.VerifyChunksAsync(Chunks(Markdown, Pdf with { Text = " " }), Container, Products, Markers, default));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SearchIndexVerification.VerifyChunksAsync(Chunks(Markdown, Pdf with { ParentId = Markdown.ParentId }), Container, Products, Markers, default));
    }

    [Fact]
    public async Task CancellationIsPropagatedDuringEnumeration()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SearchIndexVerification.VerifyChunksAsync(Chunks(Markdown, Pdf), Container, Products, Markers, cancellation.Token));
    }

    [Fact]
    public async Task ReadsBothIndexerStatusesAndEveryExpectedChunkThroughTheSdk()
    {
        using var handler = new SearchHandler();
        using var http = new HttpClient(handler);
        var options = new SearchClientOptions { Transport = new HttpClientTransport(http) };
        options.Retry.MaxRetries = 0;
        var endpoint = new Uri("https://example.search.windows.net");
        var credential = new TestCredential();
        var indexers = new SearchIndexerClient(endpoint, credential, options);
        var index = new SearchClient(endpoint, "fictional", credential, options);

        await SearchIndexVerification.VerifyAsync(indexers, index, "fictional-markdown", "fictional-pdf",
            Start.AddMinutes(-1), Start.AddMinutes(-1), Container, Products, Markers, default);
        Assert.Equal(2, handler.StatusRequests);
        Assert.Equal(1, handler.SearchRequests);
        Assert.Contains("chunk_id", handler.SearchBody, StringComparison.Ordinal);
        Assert.Contains("source", handler.SearchBody, StringComparison.Ordinal);

        handler.ReportedCount = 3;
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SearchIndexVerification.VerifyAsync(indexers, index, "fictional-markdown", "fictional-pdf",
                Start.AddMinutes(-1), Start.AddMinutes(-1), Container, Products, Markers, default));
    }

    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class SearchHandler : HttpMessageHandler
    {
        public int StatusRequests { get; private set; }
        public int SearchRequests { get; private set; }
        public int ReportedCount { get; set; } = 2;
        public string SearchBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string content;
            if (request.Method == HttpMethod.Get)
            {
                StatusRequests++;
                content = """
                    {"status":"running","lastResult":{"status":"success","startTime":"2026-01-01T00:00:00Z",
                    "endTime":"2026-01-01T00:01:00Z","itemsProcessed":2,"itemsFailed":0,"errors":[],"warnings":[]},"executionHistory":[]}
                    """;
            }
            else
            {
                SearchRequests++;
                SearchBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                content = $$"""
                    {"@odata.count":{{ReportedCount}},"value":[
                    {"chunk_id":"chunk-1","parent_id":"parent-1","title":"guide.md","source":"https://example.blob.core.windows.net/corpus/markdown/guide.md","chunk":"Fictional markdown."},
                    {"chunk_id":"chunk-2","parent_id":"parent-2","title":"exhibit.pdf","source":"https://example.blob.core.windows.net/corpus/pdf/exhibit.pdf","chunk":"The PF-21 exhibit is teal."}]}
                    """;
            }

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("x-ms-request-id", "test-request");
            return response;
        }
    }

    private static SearchIndexerStatus Status(
        IndexerStatus overall = IndexerStatus.Running,
        IndexerExecutionStatus execution = IndexerExecutionStatus.Success,
        int items = 2,
        int failed = 0,
        bool warning = false)
    {
        SearchIndexerWarning[] warnings = warning
            ? [SearchModelFactory.SearchIndexerWarning("message", "key", "name", "details", "documentationLink")]
            : [];
        var result = SearchModelFactory.IndexerExecutionResult(execution, null, Start, Start.AddMinutes(1),
            Array.Empty<SearchIndexerError>(), warnings, items, failed, null, null);
        return SearchModelFactory.SearchIndexerStatus(overall, result, Array.Empty<IndexerExecutionResult>(), null);
    }

    private static async IAsyncEnumerable<SearchChunk> Chunks(
        SearchChunk first, SearchChunk? second = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return first;
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        if (second is not null)
        {
            yield return second;
        }
    }
}
