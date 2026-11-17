using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using AiAgents.Application;

namespace AiAgents.Web.Authentication;

public sealed class LocalSessionTicketStore(
    TimeProvider clock, ILogger<LocalSessionTicketStore> logger, Guid tenantId) : ITicketStore, IDisposable
{
    private const string SessionClaim = "aiagents:local-session";
    private readonly object _gate = new();
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private bool _disposed;

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var owner = EntraConversationOwner.ResolveOwner(ticket.Principal, tenantId);
        var expires = RequiredExpiration(ticket);
        var key = RandomNumberGenerator.GetHexString(64);
        var data = Serialize(ticket, key, expires);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var session = new Session(data, owner.Marker, expires,
                clock.CreateTimer(_ => Remove(key), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan));
            bool added = false;
            try
            {
                session.Timer.Change(Remaining(expires), Timeout.InfiniteTimeSpan);
                _sessions.Add(key, session);
                added = true;
            }
            finally
            {
                if (!added)
                {
                    session.End(logger);
                }
            }
        }

        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var owner = EntraConversationOwner.ResolveOwner(ticket.Principal, tenantId);
        var expires = RequiredExpiration(ticket);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_sessions.TryGetValue(key, out var session) || session.Expires <= clock.GetUtcNow() ||
                session.OwnerMarker != owner.Marker)
            {
                throw new AuthenticationFailureException("The application session cannot be renewed. Sign in again.");
            }

            expires = expires < session.Expires ? expires : session.Expires;
            session.Data = Serialize(ticket, key, expires);
            session.Expires = expires;
            session.Timer.Change(Remaining(expires), Timeout.InfiniteTimeSpan);
        }

        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        byte[]? data = null;
        lock (_gate)
        {
            if (_sessions.TryGetValue(key, out var session) && session.Expires > clock.GetUtcNow())
            {
                data = session.Data;
            }
        }

        if (data is null)
        {
            Remove(key);
        }

        return Task.FromResult(data is null ? null : Deserialize(data));
    }

    public Task RemoveAsync(string key)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public CancellationToken GetSessionEnded(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
        {
            throw new ConversationAccessException();
        }

        var claims = identity.FindAll(SessionClaim).ToArray();
        if (claims.Length != 1)
        {
            throw new ConversationAccessException();
        }

        string key = claims[0].Value;
        var owner = EntraConversationOwner.ResolveOwner(principal, tenantId);
        lock (_gate)
        {
            if (_sessions.TryGetValue(key, out var session) && session.Expires > clock.GetUtcNow())
            {
                if (session.OwnerMarker != owner.Marker)
                {
                    throw new ConversationAccessException();
                }

                return session.Ended;
            }
        }

        Remove(key);
        throw new ConversationAccessException();
    }

    public void Dispose()
    {
        Session[] sessions;
        lock (_gate)
        {
            _disposed = true;
            sessions = [.. _sessions.Values];
            _sessions.Clear();
        }

        foreach (var session in sessions)
        {
            session.End(logger);
        }
    }

    private DateTimeOffset RequiredExpiration(AuthenticationTicket ticket)
    {
        if (ticket.Properties.ExpiresUtc is not { } expires || expires <= clock.GetUtcNow())
        {
            throw new AuthenticationFailureException("The application session requires a future expiration.");
        }

        return expires;
    }

    private TimeSpan Remaining(DateTimeOffset expires)
    {
        var remaining = expires - clock.GetUtcNow();
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void Remove(string key)
    {
        Session? session;
        lock (_gate)
        {
            _sessions.Remove(key, out session);
        }

        // Cancellation callbacks may re-enter application code, so run them outside the registry lock.
        session?.End(logger);
    }

    private static byte[] Serialize(AuthenticationTicket ticket, string key, DateTimeOffset expires)
    {
        var copy = Deserialize(TicketSerializer.Default.Serialize(ticket));
        foreach (var identity in copy.Principal.Identities)
        {
            foreach (var claim in identity.FindAll(SessionClaim).ToArray())
            {
                identity.RemoveClaim(claim);
            }
        }

        if (copy.Principal.Identity is not ClaimsIdentity { IsAuthenticated: true } authenticated)
        {
            throw new AuthenticationFailureException("An authenticated application session is required.");
        }

        authenticated.AddClaim(new Claim(SessionClaim, key));
        copy.Properties.ExpiresUtc = expires;
        return TicketSerializer.Default.Serialize(copy);
    }

    private static AuthenticationTicket Deserialize(byte[] data) =>
        TicketSerializer.Default.Deserialize(data) ?? throw new InvalidDataException("The application session ticket is invalid.");

    private sealed class Session(byte[] data, string ownerMarker, DateTimeOffset expires, ITimer timer)
    {
        private readonly CancellationTokenSource _ended = new();
        public byte[] Data { get; set; } = data;
        public string OwnerMarker { get; } = ownerMarker;
        public DateTimeOffset Expires { get; set; } = expires;
        public ITimer Timer { get; } = timer;
        public CancellationToken Ended => _ended.Token;

        public void End(ILogger logger)
        {
            try
            {
                _ended.Cancel();
            }
            catch (AggregateException exception)
            {
                logger.LogError("Application session cancellation had {CallbackFailureCount} callback failures.",
                    exception.InnerExceptions.Count);
            }
            finally
            {
                Timer.Dispose();
                _ended.Dispose();
            }
        }
    }
}
