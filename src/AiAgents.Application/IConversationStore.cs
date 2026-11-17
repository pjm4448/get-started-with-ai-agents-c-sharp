namespace AiAgents.Application;

public sealed record StoredConversation(string Id, string? OwnerMarker);

public enum ConversationMessageRole
{
    User,
    Assistant
}

public sealed record ConversationHistoryMessage(
    string Id, ConversationMessageRole Role, string Text, IReadOnlyList<Citation> Citations);

public interface IConversationStore
{
    Task<StoredConversation> CreateAsync(string ownerMarker, CancellationToken cancellationToken);

    Task<StoredConversation?> GetAsync(string conversationId, CancellationToken cancellationToken);

    IAsyncEnumerable<ConversationHistoryMessage> GetHistoryAsync(
        string conversationId, CancellationToken cancellationToken);
}
