using System.Runtime.CompilerServices;

namespace AiAgents.Application;

public sealed class AgentConversationService(IAgentClient agentClient)
{
    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string conversationId,
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        await foreach (AgentStreamEvent item in agentClient
            .StreamAsync(conversationId, message, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
}
