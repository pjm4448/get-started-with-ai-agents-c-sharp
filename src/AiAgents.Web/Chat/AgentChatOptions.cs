namespace AiAgents.Web.Chat;

public sealed class AgentChatOptions
{
    public const int MessageCharacterLimit = 4000;
    public const int HistoryMessageLimit = 100;

    public bool Enabled { get; set; }

    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
