using System.Security.Claims;
using AiAgents.Application;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Web;

namespace AiAgents.Web.Authentication;

public sealed class EntraConversationOwner(
    AuthenticationStateProvider authenticationState, Guid expectedTenantId) : ICurrentConversationOwner
{
    public async Task<ConversationOwner> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = await authenticationState.GetAuthenticationStateAsync().WaitAsync(cancellationToken);
        if (state.User.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
        {
            throw new ConversationAccessException();
        }

        var tenantId = ReadId(identity, ClaimConstants.Tid, ClaimConstants.TenantId);
        var objectId = ReadId(identity, ClaimConstants.Oid, ClaimConstants.ObjectId);
        if (tenantId != expectedTenantId)
        {
            throw new ConversationAccessException();
        }

        return new ConversationOwner(tenantId, objectId);
    }

    private static Guid ReadId(ClaimsIdentity identity, string claimType, string mappedClaimType)
    {
        Guid? result = null;
        foreach (var claim in identity.Claims.Where(claim => claim.Type == claimType || claim.Type == mappedClaimType))
        {
            if (!Guid.TryParse(claim.Value, out var value) || value == Guid.Empty ||
                (result.HasValue && result.Value != value))
            {
                throw new ConversationAccessException();
            }

            result = value;
        }

        return result ?? throw new ConversationAccessException();
    }
}
