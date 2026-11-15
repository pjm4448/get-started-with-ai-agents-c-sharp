namespace AiAgents.Application;

public abstract record AgentStreamEvent;

public sealed record TextDelta(string Text) : AgentStreamEvent;

public sealed record CompletedText(string Text) : AgentStreamEvent;

public sealed record Citation(string Label, Uri? Source) : AgentStreamEvent;

public sealed record ToolCallStarted(string Id, string Name) : AgentStreamEvent;

public sealed record ToolCallCompleted(string Id) : AgentStreamEvent;

public sealed record StatusUpdate(string Message) : AgentStreamEvent;

public sealed record UsageInformation(long? InputTokens, long? OutputTokens) : AgentStreamEvent;

public sealed record AgentWarning(string Message) : AgentStreamEvent;

public sealed record AgentError(string Message) : AgentStreamEvent;

public sealed record AgentCompleted : AgentStreamEvent;
