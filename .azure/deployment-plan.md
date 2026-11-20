# Azure deployment plan

Status: Infrastructure provisioned - awaiting saved-agent approval

On 2026-11-19 the user approved preparing and locally compiling the minimal
Bicep while subscription-specific pricing and runtime conversation permissions
remain unresolved. The user subsequently approved validation and provisioning
of a new resource group, Foundry account and project, and model deployment.
This does not authorize role assignments, saved-agent creation, enabling chat
or model requests.

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
- The user explicitly selected `primaryLocation=eastus` and `aiLocation=eastus`
  for this environment, including future non-AI regional resources. This
  overrides the original Australia East baseline; no runtime fallback is allowed.
- The user selected gpt-5-mini with GlobalStandard and explicitly accepted
  global inference processing. This supersedes the earlier Australia-only
  requirement; East US placement does not constrain inference to East US or
  to the United States.
- The user no longer wants budget, capacity-planning or subscription-price
  review to block this development milestone. Visual Studio subscription
  credits are not assumed to cover every meter or prevent charges.
- Keep runtime, image-pull, ingestion and deployment identities separate.
- Continuous evaluation is opt-in.
- Do not modify Entra, assign roles, create an agent or run paid requests or
  evaluations without separate approval.

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
- The user selected preparation of a **new** Foundry project, rather than reuse
  of an existing project, and explicitly revised its location to East US on
  2026-11-17. No project, resource group or model deployment has been created
  for this selection at the time of that decision. The restricted development
  infrastructure was subsequently provisioned on 2026-11-20 as recorded below.
  A local azd environment now exists.
  Production federation is not configured.
- This context does not authorize Entra modifications.
- Local azd environment: `aiagents-dev-9af44b45d7`; both locations are
  `eastus`. Proposed new resource group: `rg-aiagents-dev-9af44b45d7`;
  Foundry account: `aiagents9af44b45d7`; project: `aiagents-dev`; model
  deployment: `gpt-5-mini`. The resource-group name was confirmed absent
  on 2026-11-19; account-name availability remains subject to Azure validation.
  The local SDK credential chain is not guaranteed to select the same
  identity as Azure CLI.

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
| Resource group | New, uniquely named development group | Verify name and deployment permissions before provisioning |
| Foundry account | `Microsoft.CognitiveServices/accounts`, AIServices, S0 | East US; disable local/key authentication |
| Foundry project | `Microsoft.CognitiveServices/accounts/projects` | Child of the new account; platform identity requirements still need verification |
| Model deployment | `Microsoft.CognitiveServices/accounts/deployments` | gpt-5-mini, GlobalStandard, East US; initial capacity 10 |
| Saved prompt agent | Data-plane authoring operation with a recorded name/version | Text-only, no external tools, files or retrieval; matches the existing version-pinned SDK adapter |
| Foundry-managed state | Conversations and Responses history | Review storage, retention, deletion and processing terms; New chat does not delete provider history |

Use Bicep as the infrastructure authority and azd for later orchestration.
The draft [subscription entry point](../infra/main.bicep) and
[Foundry module](../infra/modules/foundry.bicep) use the stable `2025-06-01`
resource schemas returned by the available Bicep schema tool. The newer stable
`2026-09-01` reference schemas also exist. The draft must not be treated as
deployment-ready: Azure validation and deployment permissions remain
outstanding; RBAC and saved-agent authoring are subsequent steps. It creates no role
assignments. The saved agent is not an invented ARM resource or a separately
hosted custom-code container. Aspire remains local-only; it does not generate
the production infrastructure.

The root `azure.yaml` uses azd's infrastructure-only configuration with this
existing Bicep; it intentionally contains no application service and does not
deploy the local-only Aspire AppHost or web application. The parameter file
uses the explicitly selected initial model capacity of 10 and azd environment
values for resource names and locations.

The available Azure Verified Module catalog returned an account module but no
standalone project or model-deployment module. This draft uses a small custom
module to show all three linked resources together for teaching; revisit the
account AVM before production. It uses public network access, disables local
key authentication, and gives the account and project system-assigned
identities. It does not configure those identities' permissions. Model
capacity has no default: a positive value is required, but that constraint
does not establish the service's supported minimum or step. A dedicated new
resource group name must be supplied; do not point this draft at an existing
group without reviewing the effect on its location and tags.

Local Bicep linting and compilation succeeded. The compiled template was
checked for the explicit capacity parameter, East US defaults, keyless account
configuration, identities and the three expected Foundry resources. This is
not Azure deployment validation and does not prove availability, authorization
or application functionality. The repeatable
[Bicep check](../scripts/test-foundry-bicep.ps1) runs locally and in CI.

The user approved authenticated public HTTPS access for the local milestone,
subject to tenant networking policy and validation. Private networking
would require an additional local connectivity design. No anonymous access is
proposed.

Microsoft's [Agent Service setup guidance](https://learn.microsoft.com/en-us/azure/foundry/agents/environment-setup)
describes Basic Setup as using platform-managed state storage, with projects as
the unit of sharing and isolation. It describes Standard Setup as using
customer-owned Storage, Cosmos DB and Search. This proposal is for Basic
Setup; it does not provide customer-owned conversation storage or private
agent outbound networking. Review the applicable retention and privacy terms
before storing real user data. On 2026-11-19 the user accepted Basic Setup for
synthetic development testing only.

Do not provision ACR, Container Apps, Search, customer-owned Blob storage,
Key Vault or Azure Monitor resources solely for this first text-only test.
Revisit each at its mapped retrieval/production milestone; existing local
telemetry remains available without logging prompts or responses.

## Read-only East US evidence: selected model and SKU

Observed on 2026-11-17 in the confirmed subscription, using the service-native
regional model catalog and usage commands with an explicit subscription:

| Property | Observation |
| --- | --- |
| Account kind / SKU | AIServices / S0 |
| Model / version | gpt-5-mini / 2025-08-07 |
| Lifecycle | GenerallyAvailable |
| Capabilities | Responses and agentsV2 advertised |
| Deployment SKU | GlobalStandard |
| Quota record | `OpenAI.GlobalStandard.gpt-5-mini` |
| Limit / used / remaining | **1000 / 80 / 920**, unit Count |
| Inference/SKU deprecation | 2027-07-13 |

The East US quota observation removes the earlier zero-regional-quota blocker
for this selected GlobalStandard path. It does not guarantee allocatable
capacity, project/service availability, permission coverage or successful
inference. The catalog reports a default capacity of 10, but does not report
a minimum or step; this value is a practical candidate, not a spending cap,
proof of a minimum or a guarantee of available capacity. Supply the catalog
default of 10 for the initial development deployment, subject to a fresh
availability check and deployment validation.

The user confirmed both the global-processing implications and moving future
non-AI regional resources to East US. This is approval to revise the proposal
and perform read-only checks, not to provision or activate chat. Existing
resources have not been moved.

Review Foundry conversation storage and retention under the US resource
geography, together with global inference, content safety, abuse monitoring
and diagnostics. The design no longer promises Australian residency or
US-only processing. Measure local-to-East-US latency; do not assume that
global inference routing removes the local application's network latency.

## Historical Australia East evidence: superseded regional-only investigation

Observed on 2026-11-17 for AIServices S0. Each candidate below advertises
Responses, agentsV2 and a Standard SKU:

| Model / version | Catalog lifecycle | Inference/SKU deprecation | Exact quota record | Limit / used / remaining |
| --- | --- | --- | --- | --- |
| gpt-5.6-sol / 2026-07-09 | GenerallyAvailable | 2028-01-11 | `OpenAI.Standard.gpt-5.6-sol` | **0 / 0 / 0** |
| gpt-4.1-mini / 2025-04-14 | Legacy | 2027-04-14 | `OpenAI.Standard.gpt4.1-mini` | **0 / 0 / 0** |
| gpt-4o / 2024-11-20 | Legacy | 2027-04-14 | `OpenAI.Standard.gpt-4o` | **0 / 0 / 0** |

These zero quotas blocked the earlier Australia-only proposal. The user has
since replaced that requirement with the East US GlobalStandard selection
above. Do not request quota for these superseded candidates or treat their
quota as the current East US quota.

The generic quota CLI extension was unavailable. No extension was installed
and no provider was registered for this research. The service-native
`az cognitiveservices usage list` supplied the quota observations; the regional
`az cognitiveservices model list` supplied model metadata. Both were explicitly
scoped to the confirmed subscription. No quota increase was submitted.

Standard would constrain inference to an Azure geography, not necessarily a
single region. GlobalStandard, the newly selected SKU, does not provide that
geography constraint. External tools remain excluded from the first milestone.

## Historical Australia East evidence: global model and embeddings

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

This table records Australia East observations, not East US allocation.
GlobalStandard permits global inference processing regardless of resource
placement. The earlier Australia-only exclusion was explicitly superseded;
both primaryLocation and aiLocation are now eastus in this environment's
proposal. See
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
- Microsoft's current endpoint authorization guidance confirms that
  `endpoints/interact/action` permits agent endpoint interaction under the
  default Azure RBAC scheme. It does not explicitly map the separate
  conversation create, get and list-items operations used by this application.
- The current Foundry RBAC getting-started guidance recommends Foundry User on
  the Foundry resource for the project's managed identity. This is a broader
  platform-identity recommendation, not evidence that the local SDK caller
  needs that role. Verify its necessity and scope for this Basic Setup before
  proposing an assignment.
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

### Post-provision permission verification

After separately approved provisioning and role assignments, use an isolated
test principal without inherited authoring or deployment roles. Record its
effective scopes and each result without recording tokens or message contents:

| Test | Expected outcome |
| --- | --- |
| Create a conversation with a synthetic owner marker; get it and list its items | Each operation succeeds if the proposed runtime role covers it; otherwise stop and investigate the specific denial. |
| Invoke the pinned agent and receive a streamed response | Completes with the intended runtime identity and an explicitly authorized synthetic request. |
| Access an agent outside the approved scope and create or update an agent | Denied; do not broaden the runtime role to make these operations succeed. |
| Send or restore a conversation belonging to a different application user | Application denies access even if the shared Foundry principal can read that conversation. |
| Invoke or access conversations using a principal without the proposed role | Denied; distinguish missing access from network or configuration errors. |

Agent endpoint scopes are not necessarily evaluated for the standalone
conversation operations. Record their observed scopes separately; a successful
response alone is not evidence that history permissions are least privilege.
Remove temporary test assignments after verification if they are no longer
needed. Role propagation and inherited permissions can affect results.

## Cost review

The user no longer requests a budget or subscription-specific pricing review.
The earlier USD 10/month planning figure is superseded. No paid operation is
authorized by this proposal.

The 2026-11-17 Retail Prices API search did not identify ordinary global
meters. A refined search on 2026-11-19 using the meter name `GPT 5 Mini`
found these East US USD Consumption retail meters, each per million tokens:

| Category | Meter name | Meter ID | Retail price |
| --- | --- | --- | --- |
| Input | GPT 5 Mini Inpt Glbl 1M Tokens | `6d4c4956-4f18-535d-a63a-d56d74b1327b` | USD 0.25 |
| Cached input | GPT 5 Mini cchd Inpt Glbl 1M Tokens | `4b63ead2-cde6-5970-ae20-417a7dbbfc06` | USD 0.025 |
| Output | GPT 5 Mini outpt Glbl 1M Tokens | `2d33aaf7-838f-5765-8a56-d7a7537125f5` | USD 2.00 |

These are public retail estimates, not the subscription's contractual price.
The response reported an effective start of 2025-08-01 and no next page.
Subscription-specific pricing, credits and taxes have not been verified. Do not
substitute priority, Data Zone, Batch or another model's meters.

Estimate billable input (including repeated history),
cached input where applicable, output/reasoning, and any separate service or
storage charges. For example, 100 requests with 1,000 billable input and 500
billable output tokens each would cost USD 0.125 at the observed uncached
retail rates, before other charges. As a second illustration, 1,000 requests
with 3,000 uncached input and 1,000 output tokens each would cost USD 2.75.
Neither example is a measured forecast or upper bound.

Do not assume subscription credits cover every meter. Include currency,
taxes, applicable discounts and model-specific billing rules in the review.
Budget alerts are notifications, not a hard spending cap. The message-size
limit, timeout and cooperative cancellation do not guarantee a monetary cap.
Provisioned throughput or another commitment is not proposed.

## Approval gates and implementation sequence

### All validation checks pass

- AZD installation.
- `azure.yaml` schema validation.
- Environment setup.
- Authentication check.
- Subscription/location check.
- Aspire pre-provision checks for applicable resources.
- Provision preview.
- Build verification.
- Docker build context validation where applicable.
- Package validation.
- Azure Policy validation and static RBAC review.
- Aspire post-provision checks when relevant to the selected resources.

1. The user accepted the documented restricted-script source-audit gap for
   this narrow text-only development milestone on 2026-11-19. Do not retry
   denied inspections; this does not close the broader parity audit.
2. No budget forecast or subscription-specific price review is required by
   the user. Recheck availability and use the catalog's capacity of 10 as the
   initial deployment candidate; no quota increase is currently demonstrated
   to be necessary.
3. Document US-geography storage and accepted global inference processing,
   exact runtime permissions, setup/runtime principal separation, resource
   names and networking. Retain the explicit East US choice for future
   non-AI regional resources; do not silently substitute another location.
4. Validate the resource mapping, model deployment and deployment permissions.
   The user approved provisioning the limited infrastructure; runtime RBAC
   remains a separately approved follow-up.
5. Validate Bicep/azd, catalog and quota. Invoke azure-validate before
   azure-deploy execution. Do
   not initialize a competing Aspire deployment.
6. Separately approve agent authoring and any required RBAC changes. Record the
   saved agent name/version without committing real environment-specific endpoints.
7. Recheck real Entra browser sign-in/sign-out with current server-side
   sessions. Then separately approve enabling local chat and a bounded,
   synthetic-data live test. Verify stream completion/cancellation, owned
   history and access denial with the intended runtime principal.
8. Document live findings and cleanup, then resume mapped retrieval and
   production Container Apps/ACR/OIDC milestones.

## Section 7: Validation Proof

### Provisioning verification: 2026-11-20

- The recovery `azd provision --environment aiagents-dev-9af44b45d7 --no-prompt`
  succeeded. Subscription deployment `aiagents-dev-9af44b45d7-1795160324`
  and nested resource-group deployment `foundry` both report Succeeded.
- Live management-plane reads verified East US placement, AIServices account
  type, SystemAssigned identities on account and project, disabled local/key
  authentication, public access with defaultAction Deny, bypass None, exactly
  the two approved host rules and no virtual-network rules.
- The model deployment reports Succeeded, gpt-5-mini version 2025-08-07,
  GlobalStandard and capacity 10. The project also reports Succeeded.
- `azd show` confirms the expected environment and resource-group portal
  link; no application services or public application endpoints are deployed.
  The project API endpoint was returned by Azure management-plane lookup
  and has not been copied into tracked application configuration.
- Live role queries show no assignments to the account/project identities
  at their respective resource scopes (including inherited scope checks),
  and no direct project role assignments. No role assignments were created.
- The live query for the network-restriction policy returned no evaluation
  records yet. Default-deny was verified directly, but Azure Policy compliance
  is not claimed until evaluation evidence becomes available.
- No saved agent was created, chat was not enabled, and no paid model request
  or inference health test was made. Runtime authorization, least-privilege
  denials and actual data-plane connectivity remain unverified.

### Current restricted-infrastructure preflight

- The first restricted provisioning created the resource group, account and
  model successfully, but project creation failed with RequestConflict:
  another operation was in progress on the parent account. The module now
  orders project creation after model provisioning; the contract check
  verifies that dependency. Revalidate the changed template before retrying.
  No resource deletion, role assignment or live model request is required
  for this recovery.
- Recovery validation: Bicep lint/compilation and the new ordering regression
  check pass; ARM validation again reports Succeeded. The recovery preview
  skips the group and account, creates only the missing project, and shows
  model differences for service-populated currentCapacity, raiPolicyName and
  versionUpgradeOption omitted from the template. Live inspection confirms
  capacity remains 10, policy Microsoft.DefaultV2 and upgrade option
  OnceNewDefaultVersionAvailable; the requested version and GlobalStandard
  SKU are unchanged. No external resources or deletes are in the preview.
  The earlier successful build, YAML, policy, identity and packaging checks
  remain applicable; the recovery changes only child-resource ordering.
- `azd version`: 1.37.3. `azd auth login --check-status`: authenticated.
- The Azure MCP `validate_azure_yaml` command reports the infrastructure-only
  azd file valid against the stable schema.
- Named non-secret azd settings match the previously approved subscription,
  locations and resource names; the exact two host rules were checked locally
  without dumping the environment.
- `scripts/test-foundry-bicep.ps1`: lint, compilation, template contract
  and negative IP-input checks passed. `scripts/check-no-python.ps1` and
  `git diff --check` passed.
- `azd provision --preview --environment aiagents-dev-9af44b45d7 --no-prompt`:
  exactly four creates (resource group, account, model deployment and project),
  with no changes or deletions of existing resources.
- `az deployment sub validate`: Succeeded with the approved restricted
  inputs. The first invocation failed locally on Windows JSON argument
  quoting; corrected escaping succeeded without changing template inputs.
- `dotnet build --configuration Release --no-restore`: succeeded with zero
  warnings and errors. No application behavior changed in this step.
- `azd package --no-prompt`: succeeded, with no deployable application services.
- No Node.js runtime, Docker build context, Functions secret storage or
  Aspire-generated Azure infrastructure is part of this deployment. Aspire
  remains local-only; its Container Apps post-provision variables do not apply.
- East US model catalog lists gpt-5-mini version 2025-08-07 as GA and offers
  GlobalStandard with default capacity 10. Current limit is 1000, usage 80.
- Subscription deployment principal has inherited Owner, Foundry User and
  Cognitive Services OpenAI User. This permits infrastructure deployment but
  cannot demonstrate least-privilege runtime behavior.
- Static RBAC review: no role-assignment resources exist in the deployment.
  Account/project system identities perform no external data operations in
  this text-only scope; no roles are added automatically. Runtime access and
  saved-agent creation remain separately gated.
- The subscription assigns only the Microsoft cloud security benchmark at
  the inspected scope. Its network-access rule now matches the proposed Deny
  default. ARM validation succeeded; live compliance evaluation remains a
  post-provision check, not an inference from this result.

### Historical validation and permission issue

- On 2026-11-20 the user selected Manual permissions. Narrow reads of explicitly
  named non-secret azd settings succeeded without retrying the denied full
  environment dump. Subscription, resource names and both East US locations
  match the approved context. Both user-approved host addresses were stored
  only in the local azd environment and independently checked for canonical
  IPv4 syntax, distinctness and equality with the approved inputs.
- Fresh checks confirm the proposed resource group does not exist and the
  GlobalStandard gpt-5-mini quota is 1000 with 80 used. The applicable
  subscription assignment remains the Microsoft cloud security benchmark.
  Its network-access rule audits public accounts unless their network default
  action is Deny, which the changed template now specifies. This is a static
  rule assessment, not post-deployment policy compliance evidence.
- Bicep lint, compilation, template/IP contract checks, the zero-Python scan
  and whitespace checks passed again. Fresh Azure validation and preview are
  still required. This infrastructure-only azd project contains no Node.js
  application, application container or deployed Aspire resources.
- The Foundry template now specifies deny-by-default network ACLs, two
  required address parameters, and no trusted-service bypass. Local Bicep lint,
  compilation, and template contract checks pass. The string length constraints
  do not prove that the values are distinct IPv4 hosts; an independent
  input-validation script checks their syntax and distinctness. It does not
  read the azd environment or establish public routability. Verify the actual
  environment inputs before any provisioning. No post-change Azure validation or policy
  compliance result has been established.
- Earlier access to the full local azd environment values was denied by the execution
  permission system. Do not retry or bypass that denial. Environment
  configuration, substitution checks, preview, Azure validation and deployment
  were blocked; named non-secret access has now succeeded as recorded above.
  No new Azure resources,
  role assignments, agents or live model requests were created in this step.
- On 2026-11-19 the local Bicep lint, compilation and template checks passed;
  Azure CLI ARM validation returned `Succeeded`. The azd provision preview
  showed only the new resource group, account, model deployment and project.
- The azd YAML schema validated, azd packaging reported success with no
  application services, and the .NET Release build passed without warnings.
- Azure CLI reported the selected subscription and a fresh model quota of
  1000 limit, 80 used. The signed-in CLI user's inherited subscription roles
  include Owner and Foundry User; that identity cannot demonstrate
  least-privilege runtime access.
- The subscription assigns the Microsoft cloud security benchmark with its
  default Azure AI Services network restriction policy set to `Audit`.
  The rule audits accounts with public access enabled and a network default
  action other than `Deny`. The current unrestricted public Bicep would
  therefore be reported noncompliant. Resolve this policy issue before
  provisioning; an ARM validation success is not proof of audit compliance.
- At the time of that assessment the user had not provided an IP allowlist.
  No subscription-scope policy exemptions were found. The user has since
  supplied two addresses for local configuration; the changed template still
  requires fresh validation and policy evaluation.
- The Azure MCP policy query failed because its credential used the wrong
  tenant. The explicitly scoped Azure CLI policy query succeeded instead.

Current outcome: the dedicated resource group, Foundry account, project and
model are provisioned. The local azd environment exists. No agent or role
assignment was created; chat remains disabled, production startup remains
blocked, and no chargeable model request was made.

## References

- [Deployment types and processing geography](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/deployment-types)
- [Model/Responses data privacy](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy)
- [Agent Service data privacy and security](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/agents/data-privacy-security)
- [Current Foundry RBAC roles](https://learn.microsoft.com/en-us/azure/foundry/concepts/rbac-foundry)
- [Agent endpoint authorization](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/agent-endpoint-authorization)
- [Account stable ARM schema](https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/2026-09-01/accounts)
- [Project stable ARM schema](https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/2026-09-01/accounts/projects)
- [Deployment stable ARM schema](https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/2026-09-01/accounts/deployments)
- [Retail Prices API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices)
