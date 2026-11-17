# Azure deployment plan

Status: Planning

## Scope

Migrate the source application to .NET 10 and Blazor Interactive Server.
Use Azure Container Apps, Container Registry, Microsoft Foundry, managed
identities and Bicep coordinated by azd. Aspire is for local composition.
File Search and optional Azure AI Search are both in scope.

## Constraints

- Require Microsoft Entra authentication.
- No service keys, client secrets or Python runtime.
- Default primary and AI locations to Australia East; any AI fallback must be
  explicitly selected and documented.
- Keep runtime, image-pull, ingestion and deployment identities separate.
- Continuous evaluation is opt-in.
- Do not provision resources, modify Entra or run paid evaluations without
  separate approval.

## Confirmed Azure context

- Tenant: `02bc5a75-d8b0-4b14-a4e9-7eaab281064f`.
- Subscription: Visual Studio Enterprise Subscription,
  `6a52734c-7ff9-46a9-acbe-86ed9066e308`.
- Local Entra sign-in: development certificate in the current user's
  certificate store. Azure uses managed-identity federation only.
- Local-development app registration:
  `1903ad62-2b81-43b6-bb40-786b005a8961`.
- Registered local web redirect: `https://localhost:7124/signin-oidc`.
- Approved non-exportable signing certificate exists in the current user's
  personal store; only its public certificate was uploaded to Entra.
- Verified that the registration has no client secrets or requested API
  permissions. The user verified real browser sign-in before the server-side
  ticket-store update; reverify that flow after updating. Offline browser tests
  use synthetic application sessions, not real Entra sign-in.
- The user selected preparation of a **new** Foundry project in Australia East,
  rather than reuse of an existing project. No project, resource group, azd
  environment or model deployment has been created for this selection.
  Production federation is not configured.
- This context does not authorize provisioning or Entra modifications.

## Approved local development preparation

- Foundry prerequisite setup was explicitly approved and completed, including
  the microsoft.foundry and evaluation CLI extensions. Installing an evaluation
  extension does not authorize running evaluations.
- Existing Azure CLI and azd authentication checks passed without interactive
  login or changing the active subscription.
- Isolated experimental Responses SDK usage was explicitly approved and
  documented in [ADR 003](../docs/decisions/003-foundry-invocation-sdk.md).
- The adapter and ownership-aware UI are offline-tested. Provider registration
  is available only through explicit Development opt-in; the committed chat
  default remains disabled. No live integration has been activated.

## Read-only Australia East evidence

Observed on 2026-11-17 in the confirmed subscription, using the regional
Cognitive Services model catalog and usage management APIs:

| Candidate | Catalog result | Quota limit / used / remaining |
| --- | --- | --- |
| gpt-5-mini, version 2025-08-07 | AIServices S0; GenerallyAvailable; Responses and agentsV2 advertised; GlobalStandard pay-per-token SKU | 1000 / 80 / 920 for OpenAI.GlobalStandard.gpt-5-mini |
| text-embedding-3-small, version 1 | AIServices S0; GenerallyAvailable; Standard SKU available | 350 / 0 / 350 for OpenAI.Standard.text-embedding-3-small |

Quota figures are the service's reported capacity counts, not measured request
throughput or a guarantee of immediately allocatable deployment capacity.
Embedding deployment is required only for the optional Search ingestion path.
Catalog metadata lists gpt-5-mini inference deprecation on 2027-07-13;
recheck support and a replacement path before production.

The source parameter file's gpt-5-mini version 2025-08-07 matches this catalog
entry; the source Bicep's default 2024-07-18 does not. Do not copy the latter.

GlobalStandard permits global inference processing. Australia East resource
placement does not establish Australia-only inference residency. Global
processing acceptance, final capacity and costs remain unapproved. No alternate
region has been selected; both primaryLocation and aiLocation remain
australiaeast in the proposed design. See
[ADR 004](../docs/decisions/004-australia-east-model-selection.md).

## Remaining work

- Complete the source audit and record inaccessible script behavior as an
  explicit limitation.
- Validate the remaining SDK operations, resource APIs, roles and service
  availability. Refresh model and quota evidence immediately before deployment.
- Confirm GlobalStandard processing residency or choose and verify a different
  regional model; do not silently substitute either a model or region.
- Select the resource group and remaining deployment context before live
  validation.
- Define infrastructure, immutable image deployment and verification.
