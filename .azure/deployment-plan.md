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
  permissions. Local signing was verified; browser sign-in was not.
- Resource group and Foundry project remain unselected. Production federation
  is not configured.
- This context does not authorize provisioning or Entra modifications.

## Remaining work

- Complete the source audit and record inaccessible script behavior as an
  explicit limitation.
- Validate current SDKs, model availability, resource APIs, roles and quota.
- Select the resource group and remaining deployment context before live
  validation.
- Define infrastructure, immutable image deployment and verification.
