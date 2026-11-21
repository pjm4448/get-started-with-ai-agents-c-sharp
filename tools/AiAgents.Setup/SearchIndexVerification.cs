using System.Runtime.CompilerServices;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace AiAgents.Setup;

public sealed record SearchChunk(string Id, string ParentId, string Title, string Source, string Text);

public static class SearchIndexVerification
{
    public static void VerifyIndexer(SearchIndexerStatus status, DateTimeOffset? previousStart)
    {
        ArgumentNullException.ThrowIfNull(status);
        var result = status.LastResult;
        if (status.Status != IndexerStatus.Running ||
            result is not { Status: IndexerExecutionStatus.Success, StartTime: not null, EndTime: not null } ||
            result.EndTime < result.StartTime ||
            previousStart is not null && result.StartTime <= previousStart ||
            result.ItemCount <= 0 || result.FailedItemCount != 0 ||
            result.Errors.Count != 0 || result.Warnings.Count != 0 ||
            !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            throw new InvalidDataException("The Search indexer has not completed a fresh, successful execution without errors or warnings.");
        }
    }

    public static async Task VerifyAsync(
        SearchIndexerClient indexers,
        SearchClient index,
        string markdownIndexer,
        string pdfIndexer,
        DateTimeOffset? previousMarkdownStart,
        DateTimeOffset? previousPdfStart,
        Uri container,
        VerifiedProducts products,
        IReadOnlyDictionary<string, IReadOnlyList<string>> requiredText,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(indexers);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentException.ThrowIfNullOrWhiteSpace(markdownIndexer);
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfIndexer);
        if (string.Equals(markdownIndexer, pdfIndexer, StringComparison.Ordinal))
        {
            throw new ArgumentException("Markdown and PDF verification require separate indexers.");
        }
        VerifyIndexer((await indexers.GetIndexerStatusAsync(markdownIndexer, cancellationToken)).Value, previousMarkdownStart);
        VerifyIndexer((await indexers.GetIndexerStatusAsync(pdfIndexer, cancellationToken)).Value, previousPdfStart);
        await VerifyChunksAsync(ReadChunksAsync(index, cancellationToken), container, products, requiredText, cancellationToken);
    }

    public static async Task VerifyChunksAsync(
        IAsyncEnumerable<SearchChunk> chunks,
        Uri container,
        VerifiedProducts products,
        IReadOnlyDictionary<string, IReadOnlyList<string>> requiredText,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(requiredText);
        if (!container.IsAbsoluteUri || container.Scheme != Uri.UriSchemeHttps ||
            container.UserInfo.Length != 0 || container.Query.Length != 0 || container.Fragment.Length != 0 ||
            products.Files.Count == 0 ||
            products.Files.Any(file => !file.Name.EndsWith(".md", StringComparison.Ordinal) &&
                !file.Name.EndsWith(".pdf", StringComparison.Ordinal)))
        {
            throw new ArgumentException("A verified corpus and credential-free HTTPS container URI are required.");
        }

        string baseUri = container.AbsoluteUri.TrimEnd('/') + "/";
        var expected = products.Files.ToDictionary(
            file => file.Name,
            file => new Uri(new Uri(baseUri), (file.Name.EndsWith(".pdf", StringComparison.Ordinal) ? "pdf/" : "markdown/") + file.Name).AbsoluteUri,
            StringComparer.Ordinal);
        if (!requiredText.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Keys) ||
            requiredText.Any(pair => pair.Value.Count == 0 ||
            pair.Value.Any(string.IsNullOrWhiteSpace)))
        {
            throw new ArgumentException("Every corpus file requires at least one nonempty verification text value.", nameof(requiredText));
        }

        var found = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var remainingText = requiredText.ToDictionary(pair => pair.Key,
            pair => pair.Value.ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.Ordinal);
        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            if (ids.Count >= 999 || string.IsNullOrWhiteSpace(chunk.Id) || !ids.Add(chunk.Id) ||
                string.IsNullOrWhiteSpace(chunk.ParentId) || string.IsNullOrWhiteSpace(chunk.Text) ||
                string.IsNullOrWhiteSpace(chunk.Title) ||
                !expected.TryGetValue(chunk.Title, out string? source) ||
                !string.Equals(source, chunk.Source, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The Search index contains an unexpected, duplicate, or incomplete chunk.");
            }

            if (parents.TryGetValue(chunk.ParentId, out string? title) &&
                !string.Equals(title, chunk.Title, StringComparison.Ordinal))
            {
                throw new InvalidDataException("A Search parent identifier is shared by different source documents.");
            }

            parents[chunk.ParentId] = chunk.Title;
            found.Add(chunk.Title);
            if (remainingText.TryGetValue(chunk.Title, out var remaining))
            {
                remaining.RemoveWhere(marker => chunk.Text.Contains(marker, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!found.SetEquals(expected.Keys) || remainingText.Values.Any(markers => markers.Count != 0))
        {
            throw new InvalidDataException("The Search index is missing expected sources or verification text.");
        }
    }

    private static async IAsyncEnumerable<SearchChunk> ReadChunksAsync(
        SearchClient index, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        SearchOptions options = new() { Size = 1000, IncludeTotalCount = true };
        foreach (string field in new[] { "chunk_id", "parent_id", "title", "source", "chunk" })
        {
            options.Select.Add(field);
        }

        var response = await index.SearchAsync<SearchDocument>("*", options, cancellationToken);
        if (response.Value.TotalCount is not >= 1 or >= 1000)
        {
            throw new InvalidDataException("The Search index count is unavailable or exceeds the verification limit.");
        }

        long count = 0;
        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(cancellationToken))
        {
            count++;
            var document = result.Document;
            yield return new SearchChunk(
                GetString(document, "chunk_id"), GetString(document, "parent_id"), GetString(document, "title"),
                GetString(document, "source"), GetString(document, "chunk"));
        }

        if (count != response.Value.TotalCount)
        {
            throw new InvalidDataException("The Search index result count changed or could not be fully enumerated.");
        }
    }

    private static string GetString(SearchDocument document, string key) =>
        document.TryGetValue(key, out object? value) && value is string text
            ? text : throw new InvalidDataException("The Search index returned a missing or invalid required field.");
}
