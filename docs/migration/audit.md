# Migration audit

Status: **in progress**. This document is not a claim of functional parity or
deployment readiness. User-approved bounded offline foundation and local-chat
milestones do not close the remaining source audit gates. Complete those
decisions before broader migration, infrastructure implementation or a parity
claim.

## Baseline

The source is `get-started-with-ai-agents` at commit
`abf8f3dabb2c30c004e9851dde0dc10f98aee6b3`. Its working tree contains existing
changes to `src/pyproject.toml` and `src/requirements.txt`; the audit uses the
working-tree contents. Preserve those changes. The target's untracked
`.github` directory contains the migration specification and must be preserved.

The tracked source inventory contains 195 files: 105 in `src`, 37 in `docs`,
20 in `infra`, eight in `scripts`, eight in `.github`, two in `.vscode`,
three in `tests`, one in `.devcontainer`, and 11 at the root. This does not
include ignored local configuration or generated files.

See [frontend-inventory.md](frontend-inventory.md) and
[operations-inventory.md](operations-inventory.md) for individual frontend,
asset, development, operations, test and documentation inputs. The operations
inventory records the permission-denied script inspection. A filename-presence
check found every tracked filename represented in the audit documents; this is
not proof that every file's behavior was fully inspected.

## Observed behavior and parity requirements

| Capability | Observed source | Migration and verification |
| --- | --- | --- |
| Agent | Startup retrieves an existing named/versioned agent or creates one with a configured model and a retrieval tool. | Separate privileged, repeatable setup from request handling. Verify new and existing agent behavior. |
| Conversations | `/chat` uses a conversation and streams SSE; `/chat/history` lists recent items; the browser stores conversation and agent identifiers in cookies. | Metadata ownership gates send/history and is set at creation. Disabled-by-default UI has offline-tested creation, protected bookmarks, bounded restoration and New chat. Chromium verifies refresh, cancellation, cross-tab sign-out and bookmark separation with test-only sessions/provider fakes; live isolation remains unverified. See [testing](../testing.md). |
| Streaming | Deltas and completed messages include file or URL annotations; stream end is an explicit event. | Render typed incremental events, citations, completion, cancellation and safe errors; test interrupted streams. |
| File Search | When Search is disabled, startup uploads bundled files into a vector store and attaches File Search. | Preserve document search and citations; make setup idempotent and report upload failures. |
| AI Search | Optional setup creates blob storage content, index, data source, splitting/embedding skillset and two indexers. The agent tool requests `simple` queries despite vector and semantic index configuration and documentation describing hybrid search. | Preserve this actual mode and document any intentional extension to hybrid queries. Validate both indexers and citation output. |
| User interface | React chat supports history, message editing, markdown, theme settings and a Foundry link. | Reimplement useful behaviors in Blazor with accessible status, safely rendered markdown and resilient streaming. |
| Telemetry | Azure Monitor tracing is optional. The source also logs streamed content. | Instrument with content capture disabled by default; ensure logs do not include prompts or responses. |
| Evaluation | Separate live test creates an evaluation with task completion, adherence, tool success, violence and indirect-attack evaluators. It prints failures but asserts only completion. | Create explicitly opted-in live examples and assert meaningful agreed outcomes; unit tests use fakes. |
| Red teaming | A separate live test creates a taxonomy and evaluation with prohibited-actions risk and Flip/Base64 strategies. | Verify current .NET/REST support and provide an opt-in equivalent. Report unsupported operations explicitly. |
| Continuous evaluation | Startup creates an enabled evaluation rule, limited to five runs per hour, if no rule exists. | Make setup opt-in, with a privacy and cost warning; test duplicate-rule and failure handling. |
| Authentication | Optional basic authentication uses configured username and password. | Replace with required Entra authentication; configure tenant and app registration externally without a client secret. |

The frontend audit found that editing a user message repopulates the composer,
while the theme selector persists Light/Dark/System in browser storage.
Markdown supports GFM, math, syntax highlighting, copy, sanitized raw HTML and
an expandable thinking block. Starter messages, usage display and deletion
components exist, but their use by the active screen is not established.
New Chat currently clears all JavaScript-accessible cookies; preserve the
intended reset rather than that behavior.

## Backend source-file mapping

| Source file | Purpose and dependencies | Target / SDK | Azure dependency | Action | Verification |
| --- | --- | --- | --- | --- | --- |
| `src/api/main.py` | Web startup, configuration, client lifetime, optional tracing and static serving | ASP.NET Core startup and DI; Azure.Identity and Azure.AI.Projects | Foundry, optional Monitor | Replace | Startup/configuration/credential failure and telemetry tests |
| `src/api/routes.py` | Authentication, agent details, conversation history and SSE chat | Blazor/application services and typed events; Azure.AI.Projects | Foundry agents and conversations | Split | User isolation, stream, citations, cancellation and safe-error tests |
| `src/api/blob_store_manager.py` | Create Blob container and seed files when empty | Setup/ingestion service; Azure.Storage.Blobs | Blob Storage | Port | Idempotence, overwrite avoidance and explicit failures |
| `src/api/search_index_manager.py` | Build Search index, Blob data source, skillset and indexers | Ingestion service; Azure.Search.Documents | Search, Blob and embedding model | Port | Schema, permissions, indexer and readiness tests |
| `src/gunicorn.conf.py` | Once-per-process agent and search setup, file upload, continuous evaluation | Explicit setup and optional evaluation operations; Azure.AI.Projects | Foundry, Search, Blob, model | Split | Repeat setup, both retrieval modes, evaluation opt-in and failure handling |
| `src/logging_config.py` | Console and optional file logging | Standard structured .NET logging and OpenTelemetry | Monitor optional | Replace | Verify no prompt or credential content by default |
| `src/util.py` | azd environment resolution and Foundry project-link encoding | .NET configuration and safe link service where required | Foundry link only | Replace | Configuration validation, resource ID and URL tests |
| `src/__init__.py` | Source-language package marker | None | None | Remove with justification | No runtime dependency on language package markers |
| `src/api/__init__.py` | Source-language package marker | None | None | Remove with justification | No runtime dependency on language package markers |
| `src/api/templates/index.html` | Serves frontend entry point and cache-busted bundles | Blazor host and layout; ASP.NET Core | None | Replace | Base-path, auth, static assets and cache tests |
| `src/api/data/embeddings.csv` | Legacy sample embedding rows; no repository filename reference found | No automatic import; investigate provenance and external consumers | Potential Search | Requires further investigation | Establish producer, rights and actual use before omission |

## Infrastructure findings

The source creates or reuses Foundry resources and deploys a single combined
frontend/backend Container App. Its Bicep also defines a registry, Log
Analytics workspace, workspace-based Application Insights, optional Search,
optional Storage and various resource-group-scoped roles. These are source
observations, not endorsements:

- The Foundry Search connection in `infra/core/search/search-services.bicep`
  uses an admin key. Replace with a supported managed-identity connection.
- Application Insights connection and Container Apps Log Analytics settings use
  key-bearing properties; investigate supported keyless alternatives rather
  than copying them or exposing outputs.
- Search account and runtime permissions are broad and often scoped to the
  resource group; derive assignments from actual target operations.
- The source creates Blob, File, Queue and Table service resources. Only the
  Blob path is visibly used for Search ingestion; classify other resources
  after full mapping rather than reproducing them by default.
- The default Bicep location allowlist excludes Australia East. A comment
  separately identifies Australia East as supported by one evaluator. Neither
  statement proves the configured model/SKU/version is deployable there.
- Main Bicep and parameters specify differing model versions, embedding
  dimensions and capacities. Validate the selected combination rather than
  trusting either default.

### Infrastructure file mapping

These are proposed actions, not assertions that the target exists. All Bicep
rows depend on Azure Resource Manager and require compilation and linting;
deployed behavior additionally requires separately approved live checks.

| Source file | Purpose / dependencies | Target and framework | Azure dependency | Action | Verification |
| --- | --- | --- | --- | --- | --- |
| `infra/abbreviations.json` | Shared resource prefixes; consumed by main Bicep | Focused target naming defaults; JSON/Bicep | Multiple | Consolidate | Naming and collision tests |
| `infra/main.bicep` | Subscription orchestration, parameters, resources and role assignments | `infra/main.bicep`; Bicep | Resource group and all selected resources | Replace | Compile, lint, parameter and scope validation |
| `infra/main.parameters.json` | Maps azd environment values to deployment parameters | Target parameters; azd/Bicep | All provisioned resources | Replace | Check all parameters match declarations |
| `infra/main.json` | Generated ARM deployment template | Generated Bicep output, not a second source of truth | All provisioned resources | Remove with justification | Recompile Bicep and compare required resources |
| `infra/api.bicep` | Runtime identity and app environment variables | Container App and identity modules; Bicep | ACA, managed identity, Foundry, optional Search/Storage | Split | Validate environment and role references |
| `infra/core/ai/cognitiveservices.bicep` | Foundry account, project, connections and models | Foundry and model modules; Bicep | Cognitive Services and project | Replace | Verify current supported API, connections and model deployment |
| `infra/core/host/ai-environment.bicep` | Composes AI, Storage, Search and monitoring | Focused composition in main and modules; Bicep | AI, Search, Storage, Monitor | Split | Compile and test conditional resources |
| `infra/core/host/container-app-upsert.bicep` | Preserves existing image during app update | ACA module / deployment flow; Bicep | Container Apps | Replace | Reprovision and immutable-digest deployment checks |
| `infra/core/host/container-app.bicep` | App ingress, identity, registry and scaling | ACA module; Bicep | Container Apps and ACR | Replace | Identity pull, health, ingress and scaling checks |
| `infra/core/host/container-apps-environment.bicep` | ACA environment and Log Analytics link | ACA environment module; Bicep | Container Apps and Log Analytics | Replace | Compile and check supported secure log configuration |
| `infra/core/host/container-apps.bicep` | Composes registry and ACA environment | Main/module composition; Bicep | Container Apps and ACR | Consolidate | Deployment dependency checks |
| `infra/core/host/container-registry.bicep` | Registry settings and optional diagnostics | ACR module; Bicep | Container Registry | Replace | Disable admin credentials; verify pull/push |
| `infra/core/monitor/applicationinsights-dashboard.bicep` | Optional portal dashboard for monitoring | Operational guidance or dashboard only if justified; Bicep | Azure Portal and Application Insights | Requires further investigation | Trace whether deployed and validate useful equivalents |
| `infra/core/monitor/applicationinsights.bicep` | Workspace-based Application Insights and optional dashboard | Monitoring module; Bicep | Application Insights and Log Analytics | Replace | Verify workspace binding and authenticated ingestion |
| `infra/core/monitor/loganalytics.bicep` | Log Analytics workspace and retention | Monitoring module; Bicep | Log Analytics | Replace | Verify retention and deployment |
| `infra/core/search/search-services.bicep` | Search service, identity, roles and key-based project connection | Search and identity-based connection modules; Bicep | Search and Foundry | Replace | Keyless tool and scoped-role live test |
| `infra/core/security/appinsights-access.bicep` | Monitoring role assignment | Monitoring role assignment if required; Bicep | Application Insights | Replace | Verify telemetry exporter permissions |
| `infra/core/security/registry-access.bicep` | Grants ACR pull | Registry-scoped identity assignment; Bicep | ACR | Replace | Verify pull identity and denial |
| `infra/core/security/role.bicep` | Generic resource-group role assignment | Narrowly scoped role assignments; Bicep | Azure RBAC | Replace | Check role IDs, scopes and deterministic names |
| `infra/core/storage/storage-account.bicep` | Storage account and optional Blob/File/Queue/Table services | Storage module with required private Blob resources; Bicep | Storage | Replace | Verify public access, identity and used resources |

## Source configuration mapping

These are source configuration names, not values. The target should expose
typed options and validate the combinations required by the selected mode.
Several source names appear in deployment manifests without an established
runtime consumer; do not carry them forward without verifying purpose.

| Source input | Purpose / target treatment |
| --- | --- |
| `AZURE_ENV_NAME`, `AZURE_LOCATION`, `AZURE_RESOURCE_GROUP` | azd environment and deployment scope; use separate primary and AI location inputs |
| `AZURE_PRINCIPAL_ID`, `PRINCIPAL_ID_OVERRIDE`, `PRINCIPAL_TYPE_OVERRIDE` | deployment role-assignment principal; distinguish deployment, local developer and runtime principals |
| `AZURE_EXISTING_AIPROJECT_RESOURCE_ID`, `AZURE_EXISTING_AIPROJECT_ENDPOINT` | existing Foundry project; validate endpoint/resource consistency and compatibility |
| `AZURE_AI_AGENT_NAME`, `AZURE_EXISTING_AGENT_ID`, `AZURE_AI_AGENT_ID` | agent selection; validate name/version and distinguish legacy identifier |
| `AZURE_AI_AGENT_DEPLOYMENT_NAME`, `AZURE_AI_AGENT_MODEL_NAME`, `AZURE_AI_AGENT_MODEL_FORMAT`, `AZURE_AI_AGENT_MODEL_VERSION`, `AZURE_AI_AGENT_DEPLOYMENT_SKU`, `AZURE_AI_AGENT_DEPLOYMENT_CAPACITY` | agent model/deployment configuration; verify actual parameter-to-variable mapping and supported combinations |
| `AZURE_AI_EMBED_DEPLOYMENT_NAME`, `AZURE_AI_EMBED_MODEL_NAME`, `AZURE_AI_EMBED_MODEL_FORMAT`, `AZURE_AI_EMBED_MODEL_VERSION`, `AZURE_AI_EMBED_DEPLOYMENT_SKU`, `AZURE_AI_EMBED_DEPLOYMENT_CAPACITY`, `AZURE_AI_EMBED_DIMENSIONS` | optional embedding model and vector schema; validate dimension consistency |
| `USE_AZURE_AI_SEARCH_SERVICE`, `AZURE_AI_SEARCH_ENDPOINT`, `AZURE_AI_SEARCH_INDEX_NAME`, `AZURE_AI_SEARCH_CONNECTION_NAME`, `SEARCH_CONNECTION_ID`, `AZURE_SEARCH_SERVICE_NAME`, `AZURE_SEARCH_SERVICE_SKU` | optional Search configuration and connection; use managed identity and validate complete combinations |
| `USE_STORAGE_ACCOUNT`, `AZURE_STORAGE_ACCOUNT_NAME`, `AZURE_STORAGE_ACCOUNT_SKU`, `STORAGE_ACCOUNT_RESOURCE_ID`, `AZURE_BLOB_CONTAINER_NAME` | optional storage and document ingestion; use identity-based access |
| `AZURE_AIHUB_NAME`, `AZURE_AIPROJECT_NAME`, `AZURE_AISERVICES_NAME`, `AZURE_CONTAINER_REGISTRY_NAME`, `AZURE_APPLICATION_INSIGHTS_NAME`, `AZURE_LOG_ANALYTICS_WORKSPACE_NAME`, `AZURE_KEYVAULT_NAME` | source resource naming inputs; retain only for justified target resources |
| `USE_CONTAINER_REGISTRY`, `USE_APPLICATION_INSIGHTS` | source feature toggles; ensure selected topology and outputs agree |
| `ENABLE_AZURE_MONITOR_TRACING`, `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT`, `APP_LOG_FILE` | tracing and content-logging behavior; do not record sensitive content by default |
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | runtime identity selection or deployment context depending on consumer; separate typed configurations |
| `RUNNING_IN_PRODUCTION`, `AZURE_OPENAI_API_VERSION` | source runtime mode and legacy API selection; replace with environment and verified SDK configuration |
| `WEB_APP_USERNAME`, `WEB_APP_PASSWORD` | optional basic authentication; remove in favor of Entra without retaining secrets |
| `TEMPLATE_VALIDATION_MODE`, `SERVICE_API_RESOURCE_EXISTS`, `AZD_INITIAL_ENVIRONMENT_CONFIG` | source deployment/CI hooks and state; investigate necessity and secret-free alternatives |

## Candidate identities

| Principal | Resource and operation | Candidate assignment | Evidence and remaining work |
| --- | --- | --- | --- |
| Local developer credential; later a separate web runtime user-assigned identity | Invoke a preconfigured Foundry agent and access owned conversations/history | Foundry Agent Consumer is an endpoint-only candidate, not an approved assignment | Its current sole data action is `Microsoft.CognitiveServices/accounts/AIServices/endpoints/interact/action`. This does not establish coverage for explicit project conversation create/get/history APIs. Resolve exact permissions before assigning roles. |
| Separate setup identity | Create/update agent and evaluation resources | Review project-scoped Foundry User (formerly Azure AI User) for authoring | Verify each operation and scope; its broad permissions are not a runtime fallback. Current [Foundry RBAC](https://learn.microsoft.com/en-us/azure/foundry/concepts/rbac-foundry) distinguishes modern projects from Azure AI Developer hub/workspace scenarios. |
| Separate ingestion identity | Upload documents and create Search resources | Blob Data Contributor, Search Service Contributor and other roles only as required | Verify per-operation data and management permissions. |
| Foundry **account** system-assigned identity | Access the AI Search tool | Search Index Data Contributor and Search Service Contributor on Search, per current tool prerequisites | [AI Search tool documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/ai-search) explicitly lists these roles for keyless use. Test whether a narrower supported role works; do not assume project identity or reader-only access. |
| Search service identity | Read Blob content and invoke embeddings | Blob Data Reader and the minimum supported model role | Verify keyless indexer, skillset and vectorizer configuration. |
| Image-pull identity | Pull container image | AcrPull on the registry when supported by its authorization mode | Separate from runtime and deployment identity. |
| GitHub federated principal | Provision, assign required roles and push images | Minimum deployment and registry permissions at needed scopes | Verify exact role-definition actions; role assignment itself requires explicit authority. |

The local Entra web sign-in registration is not automatically the Azure SDK
caller. Identify the selected developer principal and any inherited privileges
before testing runtime least privilege. The current text-only local proposal
does not authorize any assignment; see the
[deployment plan](../../.azure/deployment-plan.md) for current quota,
Australia-only processing, pricing and permission gates.

## SDK and architecture candidates

- .NET 10 Blazor Interactive Server as a modular monolith; add a separate API
  only if a verified source contract or independent scaling requires one.
- Aspire composes local services; Bicep remains the only production
  infrastructure definition.
- Runtime invocation now uses `Azure.AI.Extensions.OpenAI` 3.0.0, whose
  experimental Responses surface was explicitly approved for an isolated
  adapter. Offline tests verify agent/version/conversation request mapping,
  text, both retrieval-tool item types, citations, usage, safe terminal errors,
  truncation, cancellation and no automatic POST retries. The Web application
  registers it only with explicit Development opt-in; the committed default
  remains disabled. See the [SDK decision](../decisions/003-foundry-invocation-sdk.md).
- The same SDK now supplies conversation create/get/history behind an
  application authorization boundary. Ownership uses server-resolved Entra
  tenant/object claims and a stable opaque metadata marker. Missing, unowned
  and foreign conversations are denied without adoption. Offline tests cover
  identity changes, cross-tenant/user isolation and actual SDK pagination;
  local session revalidation and cancellation are now enforced through
  Development-only server-side tickets. Offline-tested UI now supports creation,
  bounded history, streaming, cancellation and session-ended presentation.
  Chromium journeys now verify main interactions with synthetic sessions and
  provider fakes. Real Entra/live provider verification and shared production sessions remain gates; see the
  [session decision](../decisions/006-local-session-revocation.md).
- `Azure.AI.Projects` remains a candidate for privileged setup, evaluations and
  red teaming. Verify its current version and exact API support before adding
  it; neither stable package numbering nor local transport tests prove service
  features are GA or regionally available.
- `Azure.Identity` 1.21.0 supplies the opt-in Development credential. Search,
  Blob and Azure Monitor SDK selection still requires verification; shared
  ServiceDefaults already supplies OpenTelemetry. Use Microsoft.Extensions.AI only
  when it adds an actual abstraction; do not add Semantic Kernel without a
  source-demonstrated need.
- Use local developer credentials for development and explicitly selected
  managed identity for Azure runtime. Avoid credential-chain fallback to
  developer credentials in production.

## Region and telemetry constraints

Default both primary and AI locations to `australiaeast`. Validate exact model
version, SKU, quotas, evaluator, Search capabilities and subscription access.
An alternative AI location must be explicitly selected; document residency,
latency, networking and cost. A global model deployment does not guarantee
that inference stays in Australia.

The Application Insights instrumentation key identifies a destination and is
not itself a security token, according to
[Microsoft's connection-string documentation](https://learn.microsoft.com/en-us/azure/azure-monitor/app/connection-strings).
That does not establish an approved configuration mechanism: confirm
authenticated ingestion and reconcile it with the specification's prohibition
on secret-bearing connection strings and infrastructure outputs.

## Outstanding gates

1. Complete a file-by-file mapping of all tracked inputs, including individual
   assets, with purpose, dependencies, target, SDK, Azure dependency, action
   and verification. Inspect or explicitly resolve the permission-denied
   script behavior; filename coverage alone is insufficient.
2. Verify exact SDK/API support, version maturity, regional/model availability,
   resource APIs, local and Azure role requirements, and existing-project reuse.
3. Produce an environment-variable mapping, resource classifications, omitted
   items with justification, and initial ADRs.
4. Review the completed audit before the main migration or Bicep implementation;
   bounded offline milestones do not establish source parity. Real Azure
   deployment, Entra changes and paid evaluations require separate approval.
