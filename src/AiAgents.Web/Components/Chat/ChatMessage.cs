namespace AiAgents.Web.Components.Chat;

public enum ChatMessageRole
{
    User,
    Assistant
}

public enum ConversationStatus
{
    Idle,
    Responding,
    Completed,
    Canceled,
    Failed
}

public sealed record ChatMessage(
    ChatMessageRole Role,
    string Text,
    IReadOnlyList<string> Citations);
