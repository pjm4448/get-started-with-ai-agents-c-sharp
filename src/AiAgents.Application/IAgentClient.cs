namespace AiAgents.Application;

public interface IAgentClient
{
    IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string conversationId,
        string message,
        CancellationToken cancellationToken);
}
