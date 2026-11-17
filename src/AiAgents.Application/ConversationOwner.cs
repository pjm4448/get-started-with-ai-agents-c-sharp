using System.Security.Cryptography;
using System.Text;

namespace AiAgents.Application;

public sealed class ConversationOwner
{
    public ConversationOwner(Guid tenantId, Guid objectId, CancellationToken sessionEnded = default)
    {
        if (tenantId == Guid.Empty || objectId == Guid.Empty)
        {
            throw new ArgumentException("Conversation ownership requires non-empty tenant and object identifiers.");
        }

        Marker = "v1:" + Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes($"aiagents:owner:v1:{tenantId:D}:{objectId:D}")));
        SessionEnded = sessionEnded;
    }

    public string Marker { get; }

    public CancellationToken SessionEnded { get; }
}

public interface ICurrentConversationOwner
{
    Task<ConversationOwner> GetAsync(CancellationToken cancellationToken);
}

public sealed class ConversationAccessException() : Exception("The conversation is not available.");
