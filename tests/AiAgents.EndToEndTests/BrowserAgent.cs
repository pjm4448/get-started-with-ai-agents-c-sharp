using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using AiAgents.Application;

namespace AiAgents.EndToEndTests;

internal sealed class BrowserAgent : IAgentClient, IConversationStore
{
    private readonly ConcurrentDictionary<string, Conversation> _conversations = [];
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _creates;
    private int _sends;
    private int _historyReads;

    public int Creates => Volatile.Read(ref _creates);
    public int Sends => Volatile.Read(ref _sends);
    public int HistoryReads => Volatile.Read(ref _historyReads);
    public bool FailResponse { get; set; }
    public TaskCompletionSource StreamEnded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken StreamToken { get; private set; }

    public void CompleteResponse() => _release.TrySetResult();

    public Task<StoredConversation> CreateAsync(string ownerMarker, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var metadata = new StoredConversation($"conv_{Interlocked.Increment(ref _creates)}", ownerMarker);
        Assert.True(_conversations.TryAdd(metadata.Id, new Conversation(metadata)));
        return Task.FromResult(metadata);
    }

    public Task<StoredConversation?> GetAsync(string conversationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_conversations.GetValueOrDefault(conversationId)?.Metadata);
    }

    public async IAsyncEnumerable<ConversationHistoryMessage> GetHistoryAsync(
        string conversationId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _historyReads);
        var conversation = _conversations[conversationId];
        ConversationHistoryMessage[] history;
        lock (conversation.Messages)
        {
            history = [.. conversation.Messages];
        }

        foreach (var message in history)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
            await Task.Yield();
        }
    }

    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string conversationId, string message, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _sends);
        StreamToken = cancellationToken;
        var conversation = _conversations[conversationId];
        lock (conversation.Messages)
        {
            conversation.Messages.Add(new ConversationHistoryMessage(
                "user", ConversationMessageRole.User, message, []));
        }

        try
        {
            yield return new TextDelta("Partial offline answer");
            yield return new ToolCallStarted("search", "document-search");
            await _release.Task.WaitAsync(cancellationToken);
            if (FailResponse)
            {
                throw new IOException("Private backend details must not reach the browser.");
            }

            var citation = new Citation("offline-guide.pdf", null);
            lock (conversation.Messages)
            {
                conversation.Messages.Add(new ConversationHistoryMessage(
                    "assistant", ConversationMessageRole.Assistant, "Completed offline answer", [citation]));
            }

            yield return new ToolCallCompleted("search");
            yield return new CompletedText("Completed offline answer");
            yield return citation;
            yield return new UsageInformation(5, 8);
            yield return new AgentCompleted();
        }
        finally
        {
            StreamEnded.TrySetResult();
        }
    }

    private sealed class Conversation(StoredConversation metadata)
    {
        public StoredConversation Metadata { get; } = metadata;
        public List<ConversationHistoryMessage> Messages { get; } = [];
    }
}
