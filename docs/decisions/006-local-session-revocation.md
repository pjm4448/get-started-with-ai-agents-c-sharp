# Development-only server-side sessions and operation cancellation

## Context

The user verified local HTTPS hosting and successful Microsoft sign-in.
Clearing a self-contained cookie is insufficient for sign-out: copies remain
usable, and retained Blazor circuits keep a principal after another tab logs
out. Closing an expired SignalR transport does not cancel application work.

The user approved an in-memory server-side ticket store for the local milestone.
No new Azure resource or production session-storage architecture was approved.

## Decision

In Development only, configure the existing Cookies scheme with an ASP.NET
Core `ITicketStore`. The protected browser cookie contains the framework's
session reference; the serialized identity ticket remains in host memory.
Generate cryptographically random session keys and never log keys, tickets,
claims, prompts or responses.

Store each ticket with its fixed expiration and a session-ended cancellation
token. A `TimeProvider` timer removes expired sessions and cancels their work
even without another HTTP request. Explicit sign-out uses the cookie handler's
normal ticket-removal path after successful antiforgery validation. Removal
is idempotent and cancels work outside the registry lock. Host disposal also
cancels active sessions. Cancellation callback failures are logged as a count
without exception content; remaining callbacks still receive cancellation.

Do not silently re-create or adopt missing sessions. Restarts lose the tickets,
and pre-update self-contained cookies are rejected. Developers sign in again.
Returning a ticket produces a fresh deserialized principal, not a shared
mutable identity. Validate tenant/object ownership when creating or renewing
a ticket. Renewal cannot switch owners, recreate revoked sessions, or extend
the original deadline; switching accounts requires signing out first.

For each conversation operation, the Web owner resolver checks both the
authenticated principal and the live session registry. Missing or revoked
sessions fail closed even if a circuit retains its former principal.
The application-owned current-owner result carries the session-ended token;
the stable ownership hash does not include that token and remains unchanged
across sign-ins.

Create, metadata lookup, history enumeration and generation link caller
cancellation with session cancellation. They check cancellation before work
and before yielding events or messages. After asynchronous authorization,
the owner marker and session token must still match the original operation.
The existing chat state model preserves partial output as canceled when its
session ends. There is no generative retry or success-shaped fallback.

## Alternatives considered

- Cookie deletion alone: leaves copied cookies and existing circuits usable.
- Checking only the current circuit principal: cannot detect another tab's
  sign-out and does not stop in-flight work.
- Periodic principal checks alone: introduce a revocation delay and still need
  a shared session authority and cancellation of application work.
- A custom cookie protocol or hand-written sign-out registry alongside full
  client-side tickets: duplicates the framework's server-side session support.
- Shared durable storage now: appropriate for multiple replicas, but adds an
  unselected dependency and infrastructure beyond the approved local milestone.

## Consequences

The local developer command and certificate configuration are unchanged.
After rebuilding/restarting, sign in again. Other tabs using that same sign-in
cannot start conversation operations after sign-out, and their active
cooperative operations are canceled. An independent sign-in has a separate
ticket and is not revoked. The Microsoft SSO session is not ended.

No package, certificate, Entra registration, Azure resource or RBAC assignment
is added. The store is registered only in Development. Production startup
remains blocked. The provider and application conversation service now have
Development-only UI wiring behind a disabled-by-default feature gate. The
workspace observes session ending while idle or streaming, disables further
input and offers a full-page sign-in link.

Offline tests cover actual cookie-handler sign-in and sign-out, replay of the
original cookie after sign-out, expired sessions, unadopted legacy cookies,
retained circuit principals, independent sign-ins, restart/disposal, renewal
boundaries, active create/lookup/history/stream cancellation and preserved
partial output. The real Blazor WebSocket expiry test remains in place.

## Risks

This store supports one running Development host, not multiple replicas or
hosts sharing a cookie. A durable shared store alone would not propagate
cancellation to an already-running operation; production requires both shared
revocation state and a cross-replica cancellation/revalidation design.

This is application-session revocation, not fresh Entra validation, directory
account disablement detection, or Microsoft SSO revocation. Tabs can retain
previously delivered content or show stale sign-in UI until refreshed; the
registry protects further conversation operations, not already-delivered data.
Existing transports are not forcibly closed immediately on sign-out, but their
session cannot authorize more conversation work. Fixed-expiration transport
closure remains separately configured.

Cancellation is cooperative. A provider may already have accepted a request,
and stopping local work does not guarantee remote execution or billing stops.
The metadata authorization check and provider request are not transactional.
Authentication and cancellation do not replace tool authorization or a reviewed
agent configuration.

## Follow-up actions

Repeat browser sign-in/sign-out with the new ticket store. Offline component
tests now cover session-ended presentation, cancellation, restoration and new
chat through the ownership-aware service. Add browser tests for multiple tabs,
expiry and actual refresh. Select the production session
store, shared data-protection configuration and cross-replica cancellation
before enabling production hosting.
