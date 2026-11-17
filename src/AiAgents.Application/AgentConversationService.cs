using System.Runtime.CompilerServices;

namespace AiAgents.Application;

public sealed class AgentConversationService(
    IAgentClient agentClient, IConversationStore conversations, ICurrentConversationOwner currentOwner)
{
    public async Task<string> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = await currentOwner.GetAsync(cancellationToken);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner.SessionEnded);
        var token = operation.Token;
        token.ThrowIfCancellationRequested();
        var conversation = await conversations.CreateAsync(owner.Marker, token);
        if (string.IsNullOrWhiteSpace(conversation.Id) || conversation.OwnerMarker != owner.Marker)
        {
            throw new InvalidDataException("The conversation store did not preserve ownership.");
        }

        await EnsureCurrentOwnerAsync(owner, token);
        return conversation.Id;
    }

    public async IAsyncEnumerable<ConversationHistoryMessage> GetHistoryAsync(
        string conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        cancellationToken.ThrowIfCancellationRequested();
        var owner = await currentOwner.GetAsync(cancellationToken);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner.SessionEnded);
        var token = operation.Token;
        await AuthorizeAsync(conversationId, owner, token);
        await foreach (var item in conversations.GetHistoryAsync(conversationId, token)
            .WithCancellation(token))
        {
            token.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string conversationId,
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        cancellationToken.ThrowIfCancellationRequested();
        var owner = await currentOwner.GetAsync(cancellationToken);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner.SessionEnded);
        var token = operation.Token;
        await AuthorizeAsync(conversationId, owner, token);

        await foreach (AgentStreamEvent item in agentClient
            .StreamAsync(conversationId, message, token)
            .WithCancellation(token))
        {
            token.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    private async Task AuthorizeAsync(string conversationId, ConversationOwner owner, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var conversation = await conversations.GetAsync(conversationId, cancellationToken);
        if (conversation is null || conversation.Id != conversationId || conversation.OwnerMarker != owner.Marker)
        {
            throw new ConversationAccessException();
        }

        await EnsureCurrentOwnerAsync(owner, cancellationToken);
    }

    private async Task EnsureCurrentOwnerAsync(ConversationOwner owner, CancellationToken cancellationToken)
    {
        // Identity can change while a provider operation is in flight.
        var latestOwner = await currentOwner.GetAsync(cancellationToken);
        if (latestOwner.Marker != owner.Marker || latestOwner.SessionEnded != owner.SessionEnded)
        {
            throw new ConversationAccessException();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
