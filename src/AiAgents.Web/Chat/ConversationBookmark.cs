using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace AiAgents.Web.Chat;

public interface IConversationBookmark
{
    Task<string?> GetAsync(string ownerMarker, CancellationToken cancellationToken);
    Task SetAsync(string ownerMarker, string conversationId, CancellationToken cancellationToken);
    Task ClearAsync(string ownerMarker, CancellationToken cancellationToken);
}

public sealed class ConversationBookmark(ProtectedSessionStorage storage) : IConversationBookmark
{
    public async Task<string?> GetAsync(string ownerMarker, CancellationToken cancellationToken)
    {
        var result = await storage.GetAsync<string>(Key(ownerMarker)).AsTask().WaitAsync(cancellationToken);
        return result.Success ? result.Value : null;
    }

    public Task SetAsync(string ownerMarker, string conversationId, CancellationToken cancellationToken) =>
        storage.SetAsync(Key(ownerMarker), conversationId).AsTask().WaitAsync(cancellationToken);

    public Task ClearAsync(string ownerMarker, CancellationToken cancellationToken) =>
        storage.DeleteAsync(Key(ownerMarker)).AsTask().WaitAsync(cancellationToken);

    private static string Key(string ownerMarker) => $"aiagents:conversation:v1:{ownerMarker}";
}
