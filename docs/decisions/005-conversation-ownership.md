# Conversation ownership in Foundry metadata

## Context

Conversation identifiers come from the client and are not proof of access.
The source browser's conversation cookies must not allow one signed-in user
to send to, or read history from, another user's conversation. Ownership must
survive refreshes, restarts and multiple application instances. The user
explicitly selected Foundry metadata with fail-closed checks.

## Decision

Use the application service as the only UI-facing conversation boundary.
It depends on a current-owner resolver and a provider-backed conversation store.
No browser-supplied owner identifier is accepted by the application service.

The Web resolver reads the current server-side `AuthenticationStateProvider`
for each operation, rather than caching the principal in a circuit service.
It requires an authenticated identity, the configured tenant, and non-empty
GUID `tid` and `oid` claims (or their Microsoft.Identity.Web mapped aliases).
Missing, malformed or conflicting claims fail closed. Claims from different
identities are not combined. Email, display name and NameIdentifier are not
ownership identifiers.

Derive the marker as `v1:` followed by the lowercase hexadecimal SHA-256 of
UTF-8 `aiagents:owner:v1:{tenant:D}:{object:D}`, using canonical GUID formatting.
Store it under `aiagents_owner` in the initial Foundry conversation creation
request. Raw tenant/user IDs are not stored in metadata. This is a stable,
pseudonymous identifier, not encryption, a secret, or a bearer credential.

Before each send or history enumeration:

1. Resolve the current authenticated owner on the server.
2. Read the conversation metadata from the configured Foundry project.
3. Require the exact requested conversation ID and matching ownership marker.
4. Resolve the owner again after the asynchronous lookup, rejecting identity
   changes before invoking the agent or reading any history items.

Creation also verifies the provider preserved ownership and re-resolves the
identity before returning the new ID. Missing conversations (provider 404),
unowned legacy conversations and other-owner conversations all raise
`ConversationAccessException` with the same safe message. They are never
adopted, modified, recreated or silently replaced. Other provider failures,
including 403, throttling and outages, propagate for safe boundary handling;
they are not converted into missing records or success.

The provider uses the real SDK's create/get/item-list operations. History is
an asynchronous, chronologically ordered, SDK-paginated stream of user and
assistant text messages with citations. It does not expose system/developer
messages, reasoning or raw tool inputs/results. Non-HTTP(S) citation links
remain labels only. Refusals use a safe message; unsupported message content
fails explicitly instead of silently losing content. Cancellation propagates
through metadata requests, history pages and generation without retries.

## Alternatives considered

- Process-local or circuit-only maps: lose ownership on restart or refresh and
  do not provide a shared authority across replicas.
- A separate durable database: provides richer access policies and listings,
  but adds infrastructure and distributed consistency for no current need.
- Guess-resistant conversation IDs alone: possession is not authorization.
- Automatically assigning an owner to an existing conversation: permits
  takeover of legacy or foreign conversations.

## Consequences

No new package, database, Azure resource or RBAC assignment is introduced.
SDK types remain in Infrastructure. The Web host registers only the scoped
identity resolver; the provider and application conversation service are
**not yet connected to a page or endpoint**, and chat remains disabled.

Offline tests exercise actual SDK serialization, metadata retention,
404-versus-other errors, pagination and cancellation. Application tests check
cross-user and cross-tenant denials, changing identity, missing ownership,
pre-cancellation and the absence of downstream calls after denial.

## Risks

This is application-enforced isolation, not per-user Foundry RBAC. Foundry
administrators or principals with direct conversation write access can alter
metadata. Keep such access restricted; never expose the app's provider identity
or metadata mutation operations to the browser. The marker must not be used
as an access token or recorded in logs.

Re-reading authentication state does not perform a fresh Entra token
validation or centrally revoke cookies. A retained Blazor circuit can still
have stale authenticated state after another tab signs out. These checks do
not terminate an operation whose authorization has already completed.
Authentication expiry, sign-out/revocation and cancellation of retained
circuit work remain separate integration gates before chat is enabled.

Metadata checks and generation are not a provider-side transaction.
Application code never changes ownership, but privileged external writes or
deletion can race a check. Cancellation or an identity change during creation
can leave an owned conversation whose ID was never returned; it is not
automatically deleted or adopted. A timeout does not prove a remote operation
had no effect. No automatic retries are configured.

Conversation ownership survives reload only when the future UI retains the
conversation ID and uses this service; browser refresh/new-chat flows and
cross-replica live behavior have not been verified. History is enumerated
lazily, not buffered here; UI paging and display-size limits remain to be
selected during UI integration. Live Foundry behavior and the runtime role's
conversation permissions remain unverified.

## Follow-up actions

Verify actual browser sign-in, session revalidation and cancellation boundaries.
Wire creation, history and streaming exclusively through the application
service with a safe error boundary. Add component/end-to-end isolation and
refresh tests, then verify metadata and least-privilege access against an
approved Foundry project. Revisit a separate durable store if sharing,
conversation discovery or richer access policies become required.
