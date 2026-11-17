# Azure deployment plan

Status: Planning - local-first proposal; not approved for provisioning

## Scope

Migrate the source application to .NET 10 and Blazor Interactive Server.
Use Azure Container Apps, Container Registry, Microsoft Foundry, managed
identities and Bicep coordinated by azd. Aspire is for local composition.
File Search and optional Azure AI Search are both in scope.

The next live milestone is narrower: the existing local application and a new
Foundry text-only, saved prompt agent. Retrieval, Container Apps, ACR and
production delivery remain later migration phases, not omitted capabilities.

## Constraints

- Require Microsoft Entra authentication.
- No service keys, client secrets or Python runtime.
- Default primary and AI locations to Australia East; any AI fallback must be
  explicitly selected and documented.
- The user requires processing to remain within Australia. GlobalStandard and
  APAC Data Zone do not satisfy that requirement.
- The monthly budget is **unset**. A blank budget response is not approval for
  any amount, including the previously suggested USD 10.
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

## Proposed first-live topology

| Component | Proposed resource or operation | Boundary |
| --- | --- | --- |
| Local web application | Existing Blazor host and existing Entra sign-in | No cloud compute deployment; keep chat disabled until activation is separately approved |
| Resource group | New, explicitly named development group | Name and deployment permissions still require review |
| Foundry account | `Microsoft.CognitiveServices/accounts`, AIServices, S0 | Australia East; disable local/key authentication |
| Foundry project | `Microsoft.CognitiveServices/accounts/projects` | Child of the new account; platform identity requirements still need verification |
| Model deployment | `Microsoft.CognitiveServices/accounts/deployments` | Regional Standard only; model/version/capacity require approval and positive quota |
| Saved prompt agent | Data-plane authoring operation with a recorded name/version | Text-only, no external tools, files or retrieval; matches the existing version-pinned SDK adapter |
| Foundry-managed state | Conversations and Responses history | Review storage, retention, deletion and processing terms; New chat does not delete provider history |

Use Bicep as the infrastructure authority and azd for later orchestration.
The stable `2026-09-01` reference schemas exist for the three Cognitive Services
resource types above. This is a proposed common schema baseline, not a compiled
or deployment-validated template. Do not copy preview examples or examples
that enable key authentication. The saved agent is not an invented ARM resource
or a separately hosted custom-code container.

The local milestone proposes authenticated public HTTPS access to Foundry,
subject to tenant networking policy and explicit review. Private networking
would require an additional local connectivity design. No anonymous access is
proposed.

Do not provision ACR, Container Apps, Search, customer-owned Blob storage,
Key Vault or Azure Monitor resources solely for this first text-only test.
Revisit each at its mapped retrieval/production milestone; existing local
telemetry remains available without logging prompts or responses.

## Read-only Australia East evidence: current regional candidates

Observed on 2026-11-17 for AIServices S0. Each candidate below advertises
Responses, agentsV2 and a Standard SKU:

| Model / version | Catalog lifecycle | Inference/SKU deprecation | Exact quota record | Limit / used / remaining |
| --- | --- | --- | --- | --- |
| gpt-5.6-sol / 2026-07-09 | GenerallyAvailable | 2028-01-11 | `OpenAI.Standard.gpt-5.6-sol` | **0 / 0 / 0** |
| gpt-4.1-mini / 2025-04-14 | Legacy | 2027-04-14 | `OpenAI.Standard.gpt4.1-mini` | **0 / 0 / 0** |
| gpt-4o / 2024-11-20 | Legacy | 2027-04-14 | `OpenAI.Standard.gpt-4o` | **0 / 0 / 0** |

**Deployment is blocked by regional quota.** Prefer investigating a quota
increase for the GA gpt-5.6-sol candidate over adopting a legacy model or
relaxing residency. This is not a model selection or authorization to submit
a quota request. Capacity, price, quality and actual saved-agent compatibility
remain unapproved. The catalog's default capacity is not proof of an allowed
minimum or available allocation.

The generic quota CLI extension was unavailable. No extension was installed
and no provider was registered for this research. The service-native
`az cognitiveservices usage list` supplied the quota observations; the regional
`az cognitiveservices model list` supplied model metadata. Both were explicitly
scoped to the confirmed subscription. No quota increase was submitted.

Standard constrains inference to the selected Azure **geography**, not
necessarily a single region. An Australian deployment therefore needs an
Australia-geography review; do not promise Australia East-only processing.
Foundry Agent Service and Responses documentation also describe state stored
in the resource geography, with model/tool processing dependent on their
configuration. Review the complete selected path, including content safety,
abuse monitoring and diagnostics, before claiming the Australia-only
requirement has been verified. External tools are excluded from this milestone.

## Historical catalog evidence: not an approved residency option

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
placement does not establish Australia-only inference residency. The user's
subsequent Australia-only requirement excludes this GlobalStandard candidate,
regardless of its reported remaining quota. No alternate region has been
selected; both primaryLocation and aiLocation remain australiaeast in the
proposed design. See
[ADR 004](../docs/decisions/004-australia-east-model-selection.md).

## Identity and RBAC proposal: assignments remain gated

The local application's Entra sign-in registration is not automatically the
Azure SDK caller. Local SDK operations use the selected developer credential;
identify that principal before proposing an assignment. A developer who also
holds authoring privileges cannot demonstrate a least-privilege runtime test
merely by using the application UI.

| Principal | Resource / scope | Role proposal | Required operation / boundary |
| --- | --- | --- | --- |
| Local SDK caller; later a separate runtime user-assigned identity | Narrowest supported project or agent scope | Foundry Agent Consumer is an endpoint-interaction candidate only; conversation/history permissions remain unresolved | Invoke the pinned agent, create/get owned conversations, enumerate history; no authoring or deployment privileges |
| Separate agent setup principal | Project scope where supported | Review Foundry User, formerly Azure AI User, for authoring only | Create/read the saved agent/version; do not grant its broad permissions to runtime |
| Infrastructure deployer, distinct from runtime | Dedicated resource group and necessary role-assignment scopes | Exact control-plane role/action set still to be approved | Create only the approved resources; role-assignment authority is a separate privilege |
| Foundry platform identity, if required by the selected configuration | Minimum supported model-resource scope | Verify the actual server-side inference operation and minimum role | Do not assume caller permissions or copy broad quickstart assignments |
| Future image-pull identity | Target registry | AcrPull, subject to registry authorization mode | Image pull only; deferred |
| Future GitHub OIDC deployment principal | Deployment group and target registry | Scoped deployment permissions plus required image-push role | Separate from runtime; no client secret; deferred |
| Future ingestion principal | Required Search/Blob resources | Operation-specific ingestion roles | Not needed for text-only invocation; deferred |

Current role-definition evidence:

- Foundry Agent Consumer (`eed3b665-ab3a-47b6-8f48-c9382fb1dad6`) has the
  `Microsoft.CognitiveServices/accounts/AIServices/endpoints/interact/action`
  data action. That alone does not establish coverage for this application's
  explicit project conversation create/get/history calls.
- Foundry User (`53ca6127-db72-4b80-b1b0-d745d6d5456d`) is the renamed
  Azure AI User role. Its broad Cognitive Services data permissions and
  key/connection-secret-related management actions make it unsuitable as an
  unexamined runtime fallback.
- Current Foundry RBAC guidance identifies Azure AI Developer with
  hub/workspace scenarios, not modern Foundry projects. Do not substitute it
  or a legacy Cognitive Services role for a verified project permission set.
- The provider operation catalog exposes AIServices Responses read/write/delete
  operations, but the inspected catalog did not establish a standalone
  conversation API mapping. Do not invent conversation data actions or assume
  legacy Assistants thread permissions apply.

Resolve the exact API-to-permission mapping through authoritative guidance
and, after separately approved setup, positive and negative authorization
tests. If a custom role is required, review its exact actions and scope before
creating it. No role definitions or assignments have been changed.

Agent invocation permissions apply at agent level, not to an individual
version. Pinning a version controls application routing, not Foundry
authorization. Review all available versions and withdraw unsafe ones rather
than assuming a corrected active version revokes access to earlier versions.
Foundry identifiers are not per-user authorization boundaries; retain the
application's conversation ownership and session checks.

## Cost review

No monthly budget or usage forecast is approved. No paid operation is
authorized by this proposal.

The public Retail Prices API query for Australia East meters containing
`5.6 sol`, following pagination, returned 16 meters on 2026-11-17. All had
global (`Gl`) SKU names. Their Australia East billing location does not make
them prices for geography-based Standard. A regional Standard price has
**not** been verified; do not use those global prices to claim an Australian
deployment estimate.

Obtain an exact regional model/SKU quote before selecting capacity. Once unit
rates are verified, estimate billable input (including repeated history),
cached input where applicable, output/reasoning, and any separate service or
storage charges. For example, 100 requests with 1,000 billable input and 500
billable output tokens each would cost `0.1 * inputPricePerMillion +
0.05 * outputPricePerMillion`, before other charges. This is an arithmetic
example, not a measured forecast.

Do not assume subscription credits cover every meter. Include currency,
taxes, applicable discounts and model-specific billing rules in the review.
Budget alerts are notifications, not a hard spending cap. The message-size
limit, timeout and cooperative cancellation do not guarantee a monetary cap.
Provisioned throughput or another commitment is not proposed.

## Approval gates and implementation sequence

1. Complete or explicitly resolve the source-audit gate, including restricted
   script behavior. Do not retry denied inspections.
2. Obtain an approved monthly budget/usage envelope and verified regional
   pricing. Decide whether to request Australia East quota for gpt-5.6-sol.
   Submit no request without approval; a quota increase is not a deployment
   approval or proof of service capacity.
3. Confirm whole-path Australian processing, exact runtime permissions,
   setup/runtime principal separation, resource names and networking.
   Any investigation of another Australian region is not selection of that
   region; an eventual fallback still requires explicit approval.
4. Present the finalized model/version/Standard SKU/capacity, costs, resource
   mapping and RBAC for implementation approval. Keep this plan in Planning
   until these blockers are resolved.
5. After approval, implement focused Bicep/azd and repeat catalog/quota checks.
   Compile/lint and invoke azure-validate before any separately approved
   azure-deploy execution. Do not initialize a competing Aspire deployment.
6. Separately approve resource provisioning, agent authoring and any required
   RBAC changes. Record the saved agent name/version without committing real
   environment-specific endpoints.
7. Recheck real Entra browser sign-in/sign-out with current server-side
   sessions. Then separately approve enabling local chat and a bounded,
   synthetic-data live test. Verify stream completion/cancellation, owned
   history and access denial with the intended runtime principal.
8. Document live findings and cleanup, then resume mapped retrieval and
   production Container Apps/ACR/OIDC milestones.

No resource, model, agent, azd environment or role assignment was created for
this proposal. Chat remains disabled, production startup remains blocked,
and no chargeable model request was made.

## References

- [Deployment types and processing geography](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/deployment-types)
- [Model/Responses data privacy](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy)
- [Agent Service data privacy and security](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/agents/data-privacy-security)
- [Current Foundry RBAC roles](https://learn.microsoft.com/en-us/azure/foundry/concepts/rbac-foundry)
- [Account stable ARM schema](https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/2026-09-01/accounts)
- [Project stable ARM schema](https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/2026-09-01/accounts/projects)
- [Deployment stable ARM schema](https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/2026-09-01/accounts/deployments)
- [Retail Prices API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices)
