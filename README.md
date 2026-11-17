# Get Started with AI Agents for C#

This migration is in progress. The application cannot be started until Entra
authentication and Foundry agent access are implemented. The current solution
contains typed agent-stream contracts and preliminary Blazor transcript and
composer components. The composer remains disabled and citations are shown
as labels only; they are not yet connected to an agent or conversation history.
An initial Entra sign-in registration is present but is not yet wired into
startup or verified against a real tenant. The registration expects the
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
managed-identity federation has not been configured. These settings alone do
not yet enable application startup, and browser sign-in has not been verified.
An initial Aspire AppHost composes the web project for local development; it
cannot produce a running application until the web startup is implemented.
The [ServiceDefaults library](src/AiAgents.ServiceDefaults/Extensions.cs) now
provides health checks, service discovery, HTTP resilience, and opt-in OTLP
trace/metric export. It is referenced by the web project but not yet invoked:
the fail-closed startup remains unchanged. See the
[local composition decision](docs/decisions/001-local-composition.md).
Once startup is implemented, the intended local command is:

```shell
dotnet run --project src\AiAgents.AppHost
```

The AppHost uses the pinned Aspire CLI bundle. On this machine, the first
launch failed because the CLI could not move its extracted bundle inside the
NuGet package cache. The Aspire dashboard and web application have therefore
not yet been verified locally.

The [migration audit](docs/migration/audit.md) records the source inventory,
open investigation items, security decisions and remaining work.

Prerequisite: .NET 10 SDK. Run the initial tests with:

```shell
dotnet test get-started-with-ai-agents-c-sharp.sln --configuration Release
```

The initial pull-request workflow restores locked dependencies, builds, tests,
checks formatting, scans repository files for common Python artifacts and
integrations, and reports vulnerable packages. The scan covers tracked and
unignored files, excluding the historical migration documents and scanner
fixtures. It does not inspect container image layers. Infrastructure,
container, and deployment checks are not yet implemented.