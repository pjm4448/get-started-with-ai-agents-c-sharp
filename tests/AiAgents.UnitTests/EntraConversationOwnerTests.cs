using System.Security.Claims;
using AiAgents.Application;
using AiAgents.Web.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Web;

namespace AiAgents.UnitTests;

public sealed class EntraConversationOwnerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ObjectId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolvesAuthenticatedStableTenantAndObjectClaimsWithoutCaching(bool mapped)
    {
        var provider = new TestAuthenticationStateProvider
        {
            User = Principal(mapped ? ClaimConstants.TenantId : ClaimConstants.Tid, TenantId.ToString(),
                mapped ? ClaimConstants.ObjectId : ClaimConstants.Oid, ObjectId.ToString())
        };
        var resolver = new EntraConversationOwner(provider, TenantId);

        var owner = await resolver.GetAsync(default);
        Assert.Equal(new ConversationOwner(TenantId, ObjectId).Marker, owner.Marker);
        Assert.DoesNotContain(TenantId.ToString(), owner.Marker);
        Assert.DoesNotContain(ObjectId.ToString(), owner.Marker);

        provider.User = new ClaimsPrincipal(new ClaimsIdentity());
        await Assert.ThrowsAsync<ConversationAccessException>(() => resolver.GetAsync(default));
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("missing-tenant")]
    [InlineData("missing-object")]
    [InlineData("malformed")]
    [InlineData("empty-guid")]
    [InlineData("other-tenant")]
    [InlineData("conflicting-alias")]
    [InlineData("split-identities")]
    public async Task RejectsMissingInvalidOrConflictingIdentity(string scenario)
    {
        var user = Principal(ClaimConstants.Tid, TenantId.ToString(), ClaimConstants.Oid, ObjectId.ToString());
        var identity = Assert.IsType<ClaimsIdentity>(user.Identity);
        switch (scenario)
        {
            case "anonymous":
                user = new ClaimsPrincipal(new ClaimsIdentity(identity.Claims));
                break;
            case "missing-tenant":
                identity.RemoveClaim(identity.FindFirst(ClaimConstants.Tid));
                break;
            case "missing-object":
                identity.RemoveClaim(identity.FindFirst(ClaimConstants.Oid));
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, ObjectId.ToString()));
                break;
            case "malformed":
                identity.AddClaim(new Claim(ClaimConstants.Oid, "invalid"));
                break;
            case "empty-guid":
                identity.RemoveClaim(identity.FindFirst(ClaimConstants.Oid));
                identity.AddClaim(new Claim(ClaimConstants.Oid, Guid.Empty.ToString()));
                break;
            case "other-tenant":
                identity.RemoveClaim(identity.FindFirst(ClaimConstants.Tid));
                identity.AddClaim(new Claim(ClaimConstants.Tid, Guid.NewGuid().ToString()));
                break;
            case "conflicting-alias":
                identity.AddClaim(new Claim(ClaimConstants.ObjectId, Guid.NewGuid().ToString()));
                break;
            case "split-identities":
                identity.RemoveClaim(identity.FindFirst(ClaimConstants.Oid));
                user.AddIdentity(new ClaimsIdentity([new Claim(ClaimConstants.Oid, ObjectId.ToString())]));
                break;
        }

        var resolver = new EntraConversationOwner(new TestAuthenticationStateProvider { User = user }, TenantId);
        await Assert.ThrowsAsync<ConversationAccessException>(() => resolver.GetAsync(default));
    }

    private static ClaimsPrincipal Principal(string tenantType, string tenant, string objectType, string objectId) =>
        new(new ClaimsIdentity([new Claim(tenantType, tenant), new Claim(objectType, objectId)], "test"));

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        public required ClaimsPrincipal User { get; set; }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(User));
    }
}
