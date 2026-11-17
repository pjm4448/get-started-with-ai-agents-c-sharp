# Get Started with AI Agents for C#

This migration is in progress. The web application now starts in Development
with Entra sign-in required for its pages and Blazor connections. Chat remains
disabled until Foundry agent integration is implemented; there is no simulated
agent fallback. Production startup remains explicitly blocked until agent
integration and deployment validation are complete.

The current solution contains typed agent-stream contracts and preliminary
Blazor transcript and composer components. Citations are shown as labels only;
they are not yet connected to an agent or conversation history.

An offline-tested [Foundry adapter](src/AiAgents.Infrastructure/FoundryAgentClient.cs)
now invokes an existing, version-pinned agent and maps text, retrieval-tool
progress, citations, usage and terminal failures to application events. Its
tests use the real SDK with an in-memory HTTP transport, not live model calls.
It is **not registered in the web host**. An offline-tested application boundary
now creates conversations with an opaque Entra tenant/user ownership marker
in Foundry metadata and checks that marker before sending messages or reading
history. Missing, unowned and other-user conversations are uniformly denied;
legacy conversations are never adopted. See the
[ownership decision and limitations](docs/decisions/005-conversation-ownership.md).
Ownership-aware UI wiring, credential selection, source audit gates,
project/model configuration and live service verification remain prerequisites
for enabling chat.

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
managed-identity federation has not been configured. Actual browser sign-in and
authorization-code redemption have not yet been verified.

The session cookie expires with the Entra sign-in token and does not slide its
expiration forward on activity. Active Blazor connections close when that
session expires; expired cookies cannot open a new connection.

The **Sign out of this app** button submits an antiforgery-protected POST and
clears the browser's application cookie. The signed-out page remains public.
This is local application sign-out, not Microsoft account sign-out: Entra's
single sign-on session is unchanged. Other open tabs' existing connections are
not immediately revoked and can remain active until their session expires.
Central session revocation and circuit user-isolation verification are still
required before enabling agent operations or production hosting.

## Local startup

Prerequisite: .NET 10 SDK and the local authentication configuration above.
On the configured workstation, start the web application directly with:

```shell
dotnet run --project src\AiAgents.Web --configuration Release --launch-profile https
```

Open `https://localhost:7124`. Use the HTTPS profile to match the registered
sign-in redirect URI. The development HTTPS certificate must be trusted;
do not bypass certificate validation. Successful sign-in currently displays
the disabled chat shell, not a working agent.

The HTTPS host has been smoke-tested with the local configuration: `/alive`
and `/health` return 200, and an anonymous page request redirects to Entra.
These probes currently check host health, not Foundry connectivity. Integration
tests also verify protected Blazor negotiation, authenticated page rendering,
antiforgery-protected local sign-out, expired-cookie rejection, actual WebSocket
closure at session expiration, and blocked Production startup without calling
live Azure services.

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

Run the current tests with:

```shell
dotnet test get-started-with-ai-agents-c-sharp.sln --configuration Release
```

The initial pull-request workflow restores locked dependencies, builds, tests,
checks formatting, scans repository files for common Python artifacts and
integrations, and reports vulnerable packages. The scan covers tracked and
unignored files, excluding the historical migration documents and scanner
fixtures. It does not inspect container image layers. Infrastructure,
container, and deployment checks are not yet implemented.

The migrated solution does not require Python.