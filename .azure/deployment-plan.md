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

## Pending

- Complete the source audit and record inaccessible script behavior as an
  explicit limitation.
- Validate current SDKs, model availability, resource APIs, roles and quota.
- Select the subscription, resource group and deployment context before live
  validation; none has been selected.
- Define infrastructure, immutable image deployment and verification.
