using System.Runtime.CompilerServices;
using System.Security.Claims;
using AiAgents.Application;
using AiAgents.Web.Authentication;
using AiAgents.Web.Components.Chat;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Web;

namespace AiAgents.UnitTests;

public sealed class LocalSessionTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ObjectId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public async Task RemovingOneSessionRevokesRetainedPrincipalsButNotAnIndependentSignIn()
    {
        using var sessions = CreateStore();
        var (key, principal) = await SignInAsync(sessions);
        var (_, otherPrincipal) = await SignInAsync(sessions);
        var firstTab = Resolver(sessions, principal);
        var secondTab = Resolver(sessions, principal.Clone());
        var independentSignIn = Resolver(sessions, otherPrincipal);
        var firstOwner = await firstTab.GetAsync(default);
        var secondOwner = await secondTab.GetAsync(default);

        await sessions.RemoveAsync(key);
        await sessions.RemoveAsync(key);

        Assert.True(firstOwner.SessionEnded.IsCancellationRequested);
        Assert.True(secondOwner.SessionEnded.IsCancellationRequested);
        Assert.Null(await sessions.RetrieveAsync(key));
        await Assert.ThrowsAsync<ConversationAccessException>(() => firstTab.GetAsync(default));
        await Assert.ThrowsAsync<ConversationAccessException>(() => secondTab.GetAsync(default));
        Assert.False((await independentSignIn.GetAsync(default)).SessionEnded.IsCancellationRequested);
    }

    [Fact]
    public async Task ANewStoreDoesNotAdoptOldTicketsOrRetainedPrincipals()
    {
        using var original = CreateStore();
        var (key, principal) = await SignInAsync(original);
        var owner = await Resolver(original, principal).GetAsync(default);
        using var restarted = CreateStore();

        Assert.Null(await restarted.RetrieveAsync(key));
        await Assert.ThrowsAsync<ConversationAccessException>(() => Resolver(restarted, principal).GetAsync(default));
        original.Dispose();
        Assert.True(owner.SessionEnded.IsCancellationRequested);
    }

    [Fact]
    public async Task APrincipalWithoutAServerSessionOrWithAChangedOwnerIsDenied()
    {
        using var sessions = CreateStore();
        var (key, principal) = await SignInAsync(sessions);
        var changed = new ClaimsPrincipal(principal.Identities.Select(identity => identity.Clone()));
        var identity = Assert.IsType<ClaimsIdentity>(changed.Identity);
        identity.RemoveClaim(identity.FindFirst(ClaimConstants.Oid));
        identity.AddClaim(new Claim(ClaimConstants.Oid, Guid.NewGuid().ToString()));

        await Assert.ThrowsAsync<ConversationAccessException>(() => Resolver(sessions, Ticket().Principal).GetAsync(default));
        await Assert.ThrowsAsync<ConversationAccessException>(() => Resolver(sessions, changed).GetAsync(default));
        Assert.NotNull(await sessions.RetrieveAsync(key));
        await Resolver(sessions, principal).GetAsync(default);
    }

    [Fact]
    public async Task RenewalCannotExtendTheDeadlineOrRecreateARevokedSession()
    {
        using var sessions = CreateStore();
        var ticket = Ticket();
        var expires = ticket.Properties.ExpiresUtc;
        string key = await sessions.StoreAsync(ticket);
        var principal = Assert.IsType<AuthenticationTicket>(await sessions.RetrieveAsync(key)).Principal;
        var token = sessions.GetSessionEnded(principal);
        var replacement = Ticket();
        replacement.Properties.ExpiresUtc = expires!.Value.AddHours(1);

        await sessions.RenewAsync(key, replacement);

        var renewed = Assert.IsType<AuthenticationTicket>(await sessions.RetrieveAsync(key));
        Assert.Equal(expires, renewed.Properties.ExpiresUtc);
        Assert.Equal(token, sessions.GetSessionEnded(renewed.Principal));
        await sessions.RemoveAsync(key);
        await Assert.ThrowsAsync<AuthenticationFailureException>(() => sessions.RenewAsync(key, replacement));
        Assert.Null(await sessions.RetrieveAsync(key));
    }

    [Fact]
    public async Task RenewalCannotSwitchUsersOrModifyTheOriginalTicket()
    {
        using var sessions = CreateStore();
        var ticket = Ticket();
        string key = await sessions.StoreAsync(ticket);
        var replacement = Ticket();
        var identity = Assert.IsType<ClaimsIdentity>(replacement.Principal.Identity);
        identity.RemoveClaim(identity.FindFirst(ClaimConstants.Oid));
        identity.AddClaim(new Claim(ClaimConstants.Oid, Guid.NewGuid().ToString()));

        await Assert.ThrowsAsync<AuthenticationFailureException>(() => sessions.RenewAsync(key, replacement));
        var stored = Assert.IsType<AuthenticationTicket>(await sessions.RetrieveAsync(key));
        Assert.Equal(ObjectId.ToString(), stored.Principal.FindFirst(ClaimConstants.Oid)?.Value);
        Assert.Equal(2, ticket.Principal.Claims.Count());
    }

    [Fact]
    public async Task MissingAndExpiredDeadlinesCannotCreateSessions()
    {
        using var sessions = CreateStore();
        var ticket = Ticket();
        ticket.Properties.ExpiresUtc = null;
        await Assert.ThrowsAsync<AuthenticationFailureException>(() => sessions.StoreAsync(ticket));
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await Assert.ThrowsAsync<AuthenticationFailureException>(() => sessions.StoreAsync(ticket));
    }

    [Theory]
    [InlineData("create", false)]
    [InlineData("lookup", false)]
    [InlineData("history", false)]
    [InlineData("stream", false)]
    [InlineData("stream", true)]
    public async Task SessionTerminationCancelsBackendWorkAndDeniesSubsequentOperations(string operation, bool expire)
    {
        using var sessions = CreateStore();
        var ticket = Ticket();
        if (expire)
        {
            ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(3);
        }

        var (key, principal) = await SignInAsync(sessions, ticket.Principal, ticket.Properties);
        var backend = new BlockingBackend(operation);
        var service = new AgentConversationService(backend, backend, Resolver(sessions, principal));
        var pending = operation switch
        {
            "create" => service.CreateAsync(),
            "history" => service.GetHistoryAsync("conversation").ToListAsync().AsTask(),
            _ => (Task)service.StreamAsync("conversation", "hello").ToListAsync().AsTask()
        };
        await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        if (!expire)
        {
            await sessions.RemoveAsync(key);
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(backend.OperationToken.IsCancellationRequested);
        int calls = backend.Calls;
        await Assert.ThrowsAsync<ConversationAccessException>(
            async () => await service.StreamAsync("conversation", "hello").ToListAsync());
        Assert.Equal(calls, backend.Calls);
        Assert.Null(await sessions.RetrieveAsync(key));
    }

    [Fact]
    public async Task RevokedTokensAreRejectedEvenIfAResolverRetainsTheOwner()
    {
        using var sessions = CreateStore();
        var (key, principal) = await SignInAsync(sessions);
        var owner = await Resolver(sessions, principal).GetAsync(default);
        var backend = new BlockingBackend("stream");
        var service = new AgentConversationService(backend, backend, new RetainedOwner(owner));
        await sessions.RemoveAsync(key);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await service.StreamAsync("conversation", "hello").ToListAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await service.GetHistoryAsync("conversation").ToListAsync());
        Assert.Equal(0, backend.Calls);
    }

    [Fact]
    public async Task SessionRevocationPreservesPartialOutputAsCanceledInTheChatSession()
    {
        using var sessions = CreateStore();
        var (key, principal) = await SignInAsync(sessions);
        var backend = new BlockingBackend("stream");
        var session = new ChatSession(
            new AgentConversationService(backend, backend, Resolver(sessions, principal)),
            NullLogger<ChatSession>.Instance, "conversation");
        Task pending = session.SendAsync("hello");
        await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await sessions.RemoveAsync(key);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ConversationStatus.Canceled, session.Status);
        Assert.Equal("Partial", session.Messages[^1].Text);
        Assert.Equal(1, backend.Calls);
    }

    internal static LocalSessionTicketStore CreateStore() =>
        new(TimeProvider.System, NullLogger<LocalSessionTicketStore>.Instance, TenantId);

    internal static async Task<(string Key, ClaimsPrincipal Principal)> SignInAsync(
        LocalSessionTicketStore sessions, ClaimsPrincipal? principal = null, AuthenticationProperties? properties = null)
    {
        var ticket = Ticket();
        string key = await sessions.StoreAsync(new AuthenticationTicket(principal ?? ticket.Principal,
            properties ?? ticket.Properties, CookieAuthenticationDefaults.AuthenticationScheme));
        return (key, Assert.IsType<AuthenticationTicket>(await sessions.RetrieveAsync(key)).Principal);
    }

    private static AuthenticationTicket Ticket() => new(
        new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimConstants.Tid, TenantId.ToString()), new Claim(ClaimConstants.Oid, ObjectId.ToString())],
            CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) },
        CookieAuthenticationDefaults.AuthenticationScheme);

    private static EntraConversationOwner Resolver(LocalSessionTicketStore sessions, ClaimsPrincipal principal) =>
        new(new RetainedAuthenticationState(principal), TenantId, sessions);

    private sealed class RetainedAuthenticationState(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class RetainedOwner(ConversationOwner owner) : ICurrentConversationOwner
    {
        public Task<ConversationOwner> GetAsync(CancellationToken cancellationToken) => Task.FromResult(owner);
    }

    private sealed class BlockingBackend(string operation) : IAgentClient, IConversationStore
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken OperationToken { get; private set; }
        public int Calls { get; private set; }

        public async Task<StoredConversation> CreateAsync(string ownerMarker, CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);
            throw new InvalidOperationException("The canceled operation must not complete.");
        }

        public async Task<StoredConversation?> GetAsync(string conversationId, CancellationToken cancellationToken)
        {
            if (operation == "lookup")
            {
                await BlockAsync(cancellationToken);
            }

            return new(conversationId, new ConversationOwner(TenantId, ObjectId).Marker);
        }

        public async IAsyncEnumerable<ConversationHistoryMessage> GetHistoryAsync(
            string conversationId, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await BlockAsync(cancellationToken);
            yield break;
        }

        public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
            string conversationId, string message, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new TextDelta("Partial");
            await BlockAsync(cancellationToken);
        }

        private Task BlockAsync(CancellationToken cancellationToken)
        {
            Calls++;
            OperationToken = cancellationToken;
            Started.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
