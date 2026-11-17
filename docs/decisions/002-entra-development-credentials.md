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
The cookie does not retain access or refresh tokens. Token acquisition has an
in-memory cache; it is not a persistent multi-replica token store.

## Alternatives considered

- Client secrets: prohibited by the migration requirements.
- Use a developer CLI token as the web client's credential: not an equivalent
  confidential-client credential.
- Use managed identity directly on a workstation: no local identity endpoint.
- Host development exclusively on Azure: avoids local certificates but does not
  satisfy the desired local Aspire workflow.
- Anonymous or fake local authentication: would not verify Entra sign-in.

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

## Follow-up actions

Separate approval was obtained for the local app registration and certificate.
The registration and non-exportable private key were created, the public
certificate was uploaded, and local signing was verified. Non-secret identifiers
were saved in this workstation's .NET user-secrets. No API permissions or client
secrets were added.

Establish managed-identity federation for Azure. Wire authentication into startup and the Blazor circuit,
then verify sign-in, authorization, sign-out, expiration, and user isolation.
No live sign-in has been verified yet.
