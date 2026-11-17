# Entra development and Azure sign-in credentials

## Context

User sign-in uses an Entra confidential web application. A developer's Azure
CLI sign-in can authenticate Azure SDK operations but is not a credential for
redeeming that web application's authorization codes. A managed-identity
endpoint is not available on an ordinary local workstation.

The user confirmed local development certificates, with managed identity in
Azure. Client secrets remain prohibited.

## Decision

In the Development environment, select a single certificate by its SHA-1
thumbprint from the current user's personal certificate store. The certificate
must have an accessible private key and its public certificate must be
registered on the Entra application.

In every other environment, select one explicitly configured user-assigned
managed identity as a federated credential. Reject any development certificate
configuration outside Development rather than silently ignoring it. Do not
configure credential fallback.

Both paths use Microsoft.Identity.Web, tenant-specific authorization-code
sign-in with PKCE, a secure session cookie, and an authenticated fallback policy.
The cookie scheme authenticates existing sessions; OpenID Connect challenges
unauthenticated requests. Pages and Blazor connections require authorization.
The cookie does not retain access or refresh tokens. Token acquisition has an
in-memory cache; it is not a persistent multi-replica token store.

Tie cookie expiration to the Entra sign-in token lifetime and disable sliding
expiration. Configure the generated Blazor hub to close connections at
authentication expiration, without registering a duplicate hub endpoint.

Provide a local sign-out POST that validates antiforgery tokens before clearing
the application cookie. Redirect to a public signed-out page with an explicit
sign-in link. Do not silently log the user straight back in or imply that
Microsoft account sign-out has occurred. This requires no new Entra redirect
URI or tenant modification.

## Alternatives considered

- Client secrets: prohibited by the migration requirements.
- Use a developer CLI token as the web client's credential: not an equivalent
  confidential-client credential.
- Use managed identity directly on a workstation: no local identity endpoint.
- Host development exclusively on Azure: avoids local certificates but does not
  satisfy the desired local Aspire workflow.
- Anonymous or fake local authentication: would not verify Entra sign-in.
- Federated Entra sign-out: defer until callback registration and live
  verification are approved; do not make unapproved tenant changes.
- Sliding sessions: would extend an active session beyond its original
  sign-in token expiration.

## Consequences

Developers need a certificate and an app registration in addition to tenant
access. Only the public certificate is uploaded to Entra. The private key
remains in the certificate store, outside the repository. No certificate,
app registration, or federation is automatically created by this code.

## Risks

Certificates require renewal and private-key access controls. Selecting a
certificate by configuration does not prove that the private key is accessible
or the public key is registered with Entra. Sign-in requires live verification.
The Development environment must not be used for an Azure production deployment.

Clearing a browser cookie does not revoke a copied cookie or another tab's
existing circuit. Those sessions remain valid until expiration. Connection
expiration does not itself cancel work in a retained circuit. Implement session
revocation, operation cancellation, and user-isolation checks before enabling
agent operations; do not treat transport closure as complete session management.

## Follow-up actions

Separate approval was obtained for the local app registration and certificate.
The registration and non-exportable private key were created, the public
certificate was uploaded, and local signing was verified. Non-secret identifiers
were saved in this workstation's .NET user-secrets. No API permissions or client
secrets were added.

Authentication is now wired into Development startup and protected Blazor
endpoints. HTTP integration tests verify anonymous challenges and authenticated
page rendering using a protected test cookie. The real local HTTPS host has
also returned an Entra challenge using workstation configuration. This does
not verify actual authorization-code redemption or browser sign-in.

Local integration tests now verify successful sign-out with the rendered
antiforgery token, rejection without a valid token, expired-cookie rejection,
and actual Blazor WebSocket closure at cookie expiration. They use local test
tickets, not real Entra tokens.

Establish managed-identity federation for Azure. Verify live sign-in, sign-out,
expiration, circuit revalidation, and user isolation before enabling agent
operations or production hosting. Production startup remains explicitly blocked.
