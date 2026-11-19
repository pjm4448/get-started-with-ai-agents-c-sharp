# Get Started with AI Agents for C#

This migration is in progress. The web application now starts in Development
with Entra sign-in required for its pages and Blazor connections. Ownership-aware
chat is wired behind `Chat:Enabled=false` and tested offline; there is no
simulated agent fallback. Production startup remains explicitly blocked until
integration and deployment validation are complete.

The Blazor chat supports owned conversation creation, authorized history
restoration, incremental responses, tool progress, citation labels, usage,
cancellation and session-ended states. Agent text is rendered as encoded plain
text, not Markdown or HTML. Citation URLs are not opened.

An offline-tested [Foundry adapter](src/AiAgents.Infrastructure/FoundryAgentClient.cs)
now invokes an existing, version-pinned agent and maps text, retrieval-tool
progress, citations, usage and terminal failures to application events. Its
tests use the real SDK with an in-memory HTTP transport, not live model calls.
It is registered only when chat is explicitly enabled in Development. The application boundary
now creates conversations with an opaque Entra tenant/user ownership marker
in Foundry metadata and checks that marker before sending messages or reading
history. Missing, unowned and other-user conversations are uniformly denied;
legacy conversations are never adopted. See the
[ownership decision and limitations](docs/decisions/005-conversation-ownership.md).
Source audit gates, approved project/model/agent configuration, required Azure
permissions and live service verification remain prerequisites for a working
live chat. No Foundry resources or agent have been provisioned by this milestone.

The stable Foundry package still exposes experimental Responses APIs. Their
isolated use was explicitly approved; see the
[SDK decision and upgrade safeguards](docs/decisions/003-foundry-invocation-sdk.md).

## Local authentication

The registration expects the
non-secret environment values `Entra__TenantId` and `Entra__ClientId`, each
a non-empty GUID. In Development it requires `Entra__CertificateThumbprint`
(40 hexadecimal characters), selecting a certificate with a private key in
`CurrentUser\My`. Upload only the certificate's public key to the Entra app
registration; never commit or export the private key into the repository.
No certificate is created or installed automatically.

Outside Development, a user-assigned managed identity is required through
`Entra__ManagedIdentityClientId`. The app registration must trust that identity
through a federated identity credential. Certificate configuration is rejected
outside Development; no local-credential fallback is configured. A local
development app registration and signing certificate have been created with
approval. This workstation's tenant ID, client ID, and certificate thumbprint
are stored using .NET user-secrets, not committed configuration. Azure
managed-identity federation has not been configured. The user has now completed
Microsoft sign-in and reached the disabled chat page on this workstation,
verifying the development certificate's authorization-code flow.

The session cookie expires with the Entra sign-in token and does not slide its
expiration forward on activity. Active Blazor connections close when that
session expires; expired cookies cannot open a new connection.

Development now uses ASP.NET Core server-side session tickets in a
single-host, in-memory store. The **Sign out of this app** button submits an
antiforgery-protected POST, revokes that session on the server and clears the
browser cookie. Copies of that cookie can no longer authenticate. Conversation
operations in other tabs using the same session are denied, and active
creation, history and streaming work receive cancellation on sign-out or
expiry. Independent sign-ins remain separate sessions.

The signed-out page remains public. This is local application sign-out, not
Microsoft account sign-out: Entra's single sign-on session is unchanged.
An existing tab can still display its previous content until refreshed; it
cannot use the revoked session for further conversation operations.

**Restarting the host requires signing in again.** Existing cookies from before
this update are also rejected rather than adopted. Repeat the browser sign-in
and sign-out check after updating. Offline tests cover the new ticket lifecycle,
but browser interaction with this change has not yet been reverified. A shared
session store and cross-replica cancellation are required before production;
see the [local session decision](docs/decisions/006-local-session-revocation.md).

## Local startup

Prerequisite: .NET 10 SDK and the local authentication configuration above.
On the configured workstation, start the web application directly with:

```shell
dotnet run --project src\AiAgents.Web --configuration Release --launch-profile https
```

Open `https://localhost:7124`. Use the HTTPS profile to match the registered
sign-in redirect URI. The development HTTPS certificate must be trusted;
do not bypass certificate validation. With the committed configuration,
successful sign-in displays the disabled chat shell, not a working agent.

### Optional Foundry configuration

Leave chat disabled until the Azure project, agent, permissions and processing
residency are approved. After that setup, supply the existing
project's HTTPS endpoint, agent name and explicit agent version through
user-secrets or environment variables:

| Configuration | Environment variable | Purpose |
| --- | --- | --- |
| `Foundry:ProjectEndpoint` | `Foundry__ProjectEndpoint` | Existing project endpoint; HTTPS without credentials, query or fragment |
| `Foundry:AgentName` | `Foundry__AgentName` | Reviewed existing agent; no runtime authoring |
| `Foundry:AgentVersion` | `Foundry__AgentVersion` | Explicit pinned agent version |
| `Chat:Enabled` | `Chat__Enabled` | Defaults to `false`; explicitly opt in only after setup approval |
| `Chat:OperationTimeout` | `Chat__OperationTimeout` | Defaults to `00:02:00`; positive and at most two minutes |

Enabled chat fails startup if required Foundry options are missing or invalid.
The credential uses `DefaultAzureCredential` with developer-tool credentials,
such as Azure CLI sign-in to the configured tenant. Environment-secret,
workload-identity, managed-identity, interactive-browser and broker credentials
are excluded for this Development-only path. Website sign-in does not sign in
the Azure SDK, and neither sign-in grants missing Foundry permissions.
There is no production credential fallback.

Opening chat or selecting **New chat** does not create a remote conversation.
The first valid message creates one. The browser stores only its protected ID
in per-tab session storage, separated by the server-resolved owner. Refresh
restores up to 100 messages after authorization; longer histories show an
explicit error instead of silently truncating. Messages are limited to 4,000
characters. **New chat** clears the saved ID and transcript, never remote
history. Storage must be available; failures are visible rather than falling
back to an untracked conversation.

**Stop response**, timeout, sign-out or session expiry preserve partial output.
Interrupted or failed operations are never automatically retried; start a new
chat before sending again. Cancellation cannot guarantee remote work or billing
stops. No browser storage or provider operation runs during prerender.
See the [chat workspace decision](docs/decisions/007-local-chat-workspace.md)
for reconnect, storage and verification limitations.

The HTTPS host has been smoke-tested with the local configuration: `/alive`
and `/health` return 200, and an anonymous page request redirects to Entra.
These probes currently check host health, not Foundry connectivity. Integration
tests also verify protected Blazor negotiation, authenticated page rendering
through real cookie-handler sign-in, antiforgery-protected local sign-out,
copied-cookie rejection after sign-out, expired-session rejection, actual
WebSocket closure at session expiration, disabled-chat isolation, enabled-chat
configuration validation, provider-free prerender and blocked Production
startup without calling live Azure services.

The Aspire AppHost composes the web project for local development.
The [ServiceDefaults library](src/AiAgents.ServiceDefaults/Extensions.cs) now
provides health checks, service discovery, HTTP resilience, and opt-in OTLP
trace/metric export and is invoked by the web host. See the
[local composition decision](docs/decisions/001-local-composition.md).
The local composition command is:

```shell
dotnet run --project src\AiAgents.AppHost
```

The AppHost uses the pinned Aspire CLI bundle. On this machine, the first
launch failed because the CLI could not move its extracted bundle inside the
NuGet package cache. The Aspire dashboard and composed startup remain
unverified; the direct web-host command above does not use that CLI bundle.
No shared package-cache permissions have been changed.

The [migration audit](docs/migration/audit.md) records the source inventory,
open investigation items, security decisions and remaining work.

## Validation

Run unit, component and integration tests without a browser installation:

```shell
dotnet test get-started-with-ai-agents-c-sharp.sln --configuration Release --filter "Category!=Browser"
```

Check the draft Foundry infrastructure locally with Azure CLI and Bicep:

```powershell
& .\scripts\test-foundry-bicep.ps1
```

This compiles and checks the template without provisioning or validating it
against Azure.

For the complete suite, build once and install the pinned Playwright Chromium
headless runtime from PowerShell:

```powershell
dotnet build get-started-with-ai-agents-c-sharp.sln --configuration Release
$env:PLAYWRIGHT_SKIP_BROWSER_GC = '1'
& .\tests\AiAgents.EndToEndTests\bin\Release\net10.0\playwright.ps1 install --only-shell chromium
dotnet test get-started-with-ai-agents-c-sharp.sln --configuration Release --no-build --no-restore
```

The browser download does not require Azure or a local Entra certificate.
The suite includes bUnit coverage and five real Chromium journeys for keyboard
submission, streaming, refresh/history, cancellation, safe failures, cross-tab
sign-out and owner-separated bookmarks. Browser tests use an isolated loopback
HTTPS host, real application cookies and test-project-only provider fakes.
They do not verify real Entra sign-in or live Foundry compatibility. See
[testing](docs/testing.md) for setup, isolation and remaining checks.

The initial pull-request workflow restores locked dependencies, builds, installs
the pinned Chromium runtime, runs all four test projects,
checks formatting, lints and compiles the Foundry Bicep draft, scans repository files for common Python artifacts and
integrations, reports vulnerable packages and publishes separate per-project
TRX results. The scan covers tracked and
unignored files, excluding the historical migration documents and scanner
fixtures. It does not inspect container image layers. Azure deployment
validation, container, and deployment checks are not yet implemented.

The migrated solution does not require Python.