# Ownership-aware local chat workspace

## Context

The user approved offline UI integration with protected per-tab storage,
4,000-character messages, a two-minute operation timeout and restoration of at
most 100 messages. Azure provisioning, live activation and paid calls were not
approved. The existing ownership and Development session boundaries must not
be bypassed by UI state or browser persistence.

## Decision

Keep Interactive Server and route all UI operations through
`AgentConversationService`. A Web-layer `ChatWorkspace` coordinates state,
bookmark restoration, deadlines, session cancellation and safe notifications;
Razor components render state and dispatch user interactions. No separate API
or new streaming transport is introduced.

Register real Foundry services only when `Chat:Enabled` is explicitly true,
only in Development, with validated endpoint/name/version options. The
committed default is false. Use the developer credential and non-retrying
transport described in [the SDK decision](003-foundry-invocation-sdk.md).
Neither startup nor prerender performs provider operations or token acquisition.

Initialize after the first interactive render, when browser storage is
available. Store only the conversation ID using ASP.NET Core
`ProtectedSessionStorage`, under an owner-derived key. The server resolves
ownership, never the browser. Storage protection is an integrity/confidentiality
measure, not authorization: every restoration and send still checks Foundry
metadata. Never adopt a missing, unowned or foreign conversation.

Opening the screen or selecting New chat does not create a conversation.
The first valid send creates one, rechecks the original owner/session, saves its
ID and then invokes generation. Creation or storage failure preserves the draft
and prevents generation. An abandoned create may leave an owned remote record;
do not delete or silently retry it.

New chat clears this owner's bookmark and local transcript only. It never
deletes provider history. Restoration buffers at most 100 user/assistant
messages; observing a 101st message fails explicitly and displays none of that
partial history. Recheck the original owner/session before presenting the
buffer. Messages longer than 4,000 characters or containing only whitespace
are rejected before provider work.

Each operation has a positive timeout no greater than two minutes. Stream
cancellation, timeout or failure preserves partial output and requires New
chat before another send. There are no automatic generative retries, including
after a connection interruption. Session ending also cancels local work,
disables input and offers a full-page sign-in link. Disposal cancels active
work and unsubscribes rendering callbacks.

Render untrusted text and citation labels with Razor encoding, not raw HTML or
Markdown. Announce loading, streaming, tool progress, errors and session ending
with semantic status/alert elements. Keep detailed exception content, prompts,
responses, IDs and owner markers out of coordinator logs.

## Alternatives considered

- Circuit-only IDs: cannot restore after refresh.
- Shared local storage: couples tabs and creates unwanted cross-tab switching.
- Unprotected browser IDs: easier to alter or disclose; metadata authorization
  would still be mandatory.
- Saving transcripts in browser storage: retains unnecessary sensitive content.
- Creating on page load/New chat: creates unused provider records.
- Truncating long history silently: misrepresents an incomplete conversation.
- Retrying or reusing an interrupted conversation automatically: may duplicate
  generation or side effects with ambiguous provider completion.
- An external API: adds no demonstrated boundary for this local milestone.

## Consequences

Offline bUnit tests cover real components and the application authorization
boundary using fake provider/storage dependencies, plus actual bookmark
protection with fake JavaScript interop. Host tests verify configuration
failure, default-disabled isolation and enabled prerender without contacting
Foundry. No mock provider is registered in the application.

The local developer command and Entra registration are unchanged. Existing
browser sign-in should be reverified after the server-side ticket change.
Enabling chat without an approved project and permissions does not make it work.

## Risks

The in-memory session store remains single-host only. Protected storage depends
on browser availability and server data-protection keys; corruption or lost
keys produces a visible error, not silent replacement. Duplicating a browser
tab can copy its initial session-storage value: per-tab storage is not a
guarantee that two tabs cannot reference the same conversation. Concurrent
generation across circuits/tabs is not yet coordinated; users should not send
concurrently to a copied conversation.

Canceling a JavaScript interop wait does not guarantee a storage write already
dispatched to the browser was canceled. Subsequent use always reauthorizes the
saved ID. Canceling local work likewise cannot guarantee remote execution or
billing stops.

Blazor reconnect can resume a retained circuit and its current work, rather
than starting another request. A new circuit restores saved history. Offline
tests do not establish browser reconnect, focus behavior, screen-reader
usability, live stream timing or service compatibility.

## Follow-up actions

Reverify browser sign-in/sign-out. Complete source audit gates and approve
project/model/agent configuration, costs and processing residency before live
activation. Verify Foundry permissions and denial behavior in the approved
environment. Add focused Playwright refresh, reconnect, cancellation and
multi-tab journeys. Implement production shared sessions, data protection and
cross-circuit concurrency policy before production hosting.
