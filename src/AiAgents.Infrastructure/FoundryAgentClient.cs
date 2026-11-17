using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Text;
using AiAgents.Application;
using Azure.Core;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

namespace AiAgents.Infrastructure;

#pragma warning disable OPENAI001, AAIP002 // Approved isolated Responses API usage; see docs/decisions/003-foundry-invocation-sdk.md.
public sealed class FoundryAgentClient : IAgentClient
{
    private readonly ProjectResponsesClient _client;

    public FoundryAgentClient(FoundryAgentOptions options, TokenCredential credential, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AgentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AgentVersion);
        if (options.ProjectEndpoint is not { IsAbsoluteUri: true, Scheme: "https", UserInfo: "", Query: "", Fragment: "" })
        {
            throw new ArgumentException("The Foundry project endpoint must be an absolute HTTPS URI without credentials, query, or fragment.", nameof(options));
        }

        var clientOptions = new ProjectResponsesClientOptions
        {
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(0),
            ClientLoggingOptions = new ClientLoggingOptions
            {
                EnableMessageLogging = false,
                EnableMessageContentLogging = false
            }
        };
        _client = new ProjectResponsesClient(
            options.ProjectEndpoint, credential, new AgentReference(options.AgentName, options.AgentVersion),
            defaultConversationId: null, options: clientOptions);
    }

    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string conversationId,
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        cancellationToken.ThrowIfCancellationRequested();
        CreateResponseOptions options = new()
        {
#pragma warning disable SCME0001 // Foundry conversation extension; isolated under the same approved ADR.
            AgentConversationId = conversationId,
#pragma warning restore SCME0001
            InputItems = { ResponseItem.CreateUserMessageItem(message) }
        };
        var completedText = new StringBuilder();

        await using var stream = await _client.CreateResponseStreamingAsync(options, cancellationToken);
        await foreach (var update in stream.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (update)
            {
                case StreamingResponseCreatedUpdate:
                    yield return new StatusUpdate("The agent is responding.");
                    break;
                case StreamingResponseOutputTextDeltaUpdate delta:
                    yield return new TextDelta(delta.Delta);
                    break;
                case StreamingResponseOutputTextDoneUpdate done:
                    completedText.Append(done.Text);
                    yield return new CompletedText(completedText.ToString());
                    break;
                case StreamingResponseOutputItemAddedUpdate { Item: FileSearchCallResponseItem search }:
                    yield return new ToolCallStarted(search.Id, "file_search");
                    break;
                case StreamingResponseOutputItemAddedUpdate { Item: AzureAISearchToolCall search }:
                    yield return new ToolCallStarted(search.Id, "azure_ai_search");
                    break;
                case StreamingResponseOutputItemAddedUpdate { Item: not MessageResponseItem and not ReasoningResponseItem }:
                    yield return new AgentError("The agent requested an unsupported output or tool.");
                    yield break;
                case StreamingResponseOutputItemDoneUpdate { Item: FileSearchCallResponseItem search }:
                    yield return new ToolCallCompleted(search.Id);
                    break;
                case StreamingResponseOutputItemDoneUpdate { Item: AzureAISearchToolCall search }:
                    yield return new ToolCallCompleted(search.Id);
                    break;
                case StreamingResponseOutputItemDoneUpdate { Item: MessageResponseItem item }:
                    foreach (var part in item.Content)
                    {
                        foreach (var annotation in part.OutputTextAnnotations)
                        {
                            switch (annotation)
                            {
                                case FileCitationMessageAnnotation file:
                                    yield return new Citation(file.Filename, null);
                                    break;
                                case UriCitationMessageAnnotation url:
                                    yield return new Citation(url.Title,
                                        url.Uri.Scheme is "https" or "http" ? url.Uri : null);
                                    break;
                            }
                        }
                    }

                    break;
                case StreamingResponseErrorUpdate:
                case StreamingResponseFailedUpdate:
                case StreamingResponseIncompleteUpdate:
                case StreamingResponseRefusalDoneUpdate:
                    yield return new AgentError("The agent could not complete its response.");
                    yield break;
                case StreamingResponseCompletedUpdate completed:
                    if (completed.Response.Usage is { } usage)
                    {
                        yield return new UsageInformation(usage.InputTokenCount, usage.OutputTokenCount);
                    }

                    yield return new AgentCompleted();
                    yield break;
            }
        }

        throw new InvalidDataException("The agent stream ended without a terminal response event.");
    }
}
#pragma warning restore OPENAI001, AAIP002
