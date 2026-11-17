namespace AiAgents.Infrastructure;

public sealed record FoundryAgentOptions
{
    public required Uri ProjectEndpoint { get; init; }

    public required string AgentName { get; init; }

    public required string AgentVersion { get; init; }
}
