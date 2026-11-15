# Migration: C#.NET Version of Get Started with AI Agents

## 1\. Objective

Migrate the functionality, architecture, educational demonstrations, infrastructure, tests, and documentation from the source repository:

get\-started\-with\-ai\-agents

into a new repository:

get\-started\-with\-ai\-agents\-c\-sharp

The target repository must provide functional and educational parity using idiomatic C\# and .NET, with:

- .NET 10 LTS.
- A Blazor Web App frontend.
- Azure Container Apps as the production hosting platform.
- Azure Container Registry for OCI container images.
- .NET Aspire for local application composition and developer orchestration.
- Bicep as the authoritative Azure infrastructure definition.
- Azure Developer CLI (azd) for provisioning and deployment.
- Managed identities and Azure RBAC instead of service API keys or client secrets.
- GitHub Actions using OpenID Connect workload identity federation.
- No Python runtime, Python source code, Python tooling, or Python\-based deployment steps in the final repository.

Do not mechanically translate Python syntax into C\#. Reimplement each capability using appropriate .NET, ASP.NET Core, Blazor, Azure SDK, dependency injection, configuration, observability, testing, and security patterns.

The result must be a production\-credible educational repository. It should remain understandable to a developer learning how to build AI agent applications with C\# and Azure.

---

## 2\. Guiding Principles

Apply these principles throughout the migration:

1. Preserve functional and educational parity rather than source\-language structure.
2. Prefer the simplest architecture that satisfies the demonstrated requirements.
3. Do not reproduce unused resources, accidental complexity, or technical debt merely because they exist in the source repository or an existing Azure resource group.
4. Use current supported Microsoft SDKs and Azure resource types.
5. Keep privileged Azure operations and credentials on the server.
6. Use managed identity and least\-privilege Azure RBAC.
7. Keep local orchestration separate from the authoritative production infrastructure definition.
8. Make architecture decisions explicit and traceable.
9. Treat build, test, security, observability, and deployment verification as part of the implementation.
10. Do not silently omit demonstrations that cannot be migrated directly. Document the limitation, proposed equivalent, and resulting parity decision.

---

## 3\. Initial Audit and Migration Plan

Before writing application or infrastructure code, analyze the source repository in full.

Produce the following artifacts:

- A repository inventory.
- A file\-by\-file migration mapping.
- A feature\-parity matrix.
- An Azure resource mapping.
- An SDK selection matrix.
- An identity and RBAC matrix.
- A list of architecture decisions and unresolved questions.
- A phased implementation plan.
- A list of source files or resources proposed for omission, with justification.

The migration mapping must cover:

- Python modules.
- Agent implementations.
- Model integrations.
- Tool and function implementations.
- Retrieval and indexing functionality.
- User interface views.
- APIs and streaming protocols.
- Configuration files.
- Environment variables.
- Infrastructure\-as\-code resources.
- Deployment scripts.
- Containers and container orchestration.
- Tests.
- GitHub Actions workflows.
- Dev container configuration.
- Documentation.
- Sample data and other supporting assets.

For every relevant source item, record:

| Source item | Current purpose | Dependencies | Proposed C\#/.NET target | Selected SDK or framework | Azure dependency | Migration action | Verification method |
| --- | --- | --- | --- | --- | --- | --- | --- |

Use one of these migration actions:

- Port.
- Replace.
- Consolidate.
- Split.
- Retain unchanged.
- Remove with justification.
- Requires further investigation.

Do not begin the main implementation until this audit and proposed migration plan have been produced.

---

## 4\. Target .NET Platform

Use .NET 10 LTS and a compatible supported C\# language version.

Apply the following repository\-wide defaults:

- Nullable reference types enabled.
- Implicit global usings enabled where appropriate.
- SDK\-style project files.
- Central package management using Directory.Packages.props.
- Shared build settings using Directory.Build.props.
- Locked or otherwise reproducible dependency restore.
- Current supported package versions.
- Repository\-owned projects configured to treat warnings as errors.
- Latest compatible Microsoft code analyzers enabled.
- Asynchronous APIs for network and I/O operations.
- CancellationToken propagated through asynchronous call chains.
- Dependency injection through Microsoft.Extensions.DependencyInjection.
- Typed options using Microsoft.Extensions.Options.
- Structured logging using Microsoft.Extensions.Logging.
- Configuration through the standard .NET configuration system.
- Secret storage excluded from source control.

Use modern C\# features where they improve clarity, including:

- File\-scoped namespaces.
- Primary constructors where they remain readable.
- Records for immutable messages and event models.
- Pattern matching.
- Required members where appropriate.
- Collection expressions where compatible and readable.
- Minimal APIs for small, focused HTTP surfaces.

Do not use a language feature merely because it is new. Prefer readability and educational clarity over novelty.

Do not suppress compiler or analyzer warnings merely to make the build pass. Every suppression must be narrow, documented, and justified.

---

## 5\. Target Solution Structure

Use a conventional structure similar to the following, adjusting it only when the source audit identifies a better justified boundary:

/

├── .github/

│   └── workflows/

├── docs/

│   ├── architecture/

│   ├── decisions/

│   ├── deployment/

│   └── migration/

├── infra/

│   ├── main.bicep

│   ├── main.parameters.json

│   ├── modules/

│   └── abbreviations.json

├── src/

│   ├── AiAgents.AppHost/

│   ├── AiAgents.ServiceDefaults/

│   ├── AiAgents.Web/

│   ├── AiAgents.Application/

│   ├── AiAgents.Domain/

│   ├── AiAgents.Infrastructure/

│   └── AiAgents.Api/

├── tests/

│   ├── AiAgents.UnitTests/

│   ├── AiAgents.ComponentTests/

│   ├── AiAgents.IntegrationTests/

│   └── AiAgents.EndToEndTests/

├── azure.yaml

├── Directory.Build.props

├── Directory.Packages.props

├── global.json

├── README.md

└── get\-started\-with\-ai\-agents\-c\-sharp.sln

The separate Agent API is not mandatory by default.

Start with a modular monolith unless the audit establishes a concrete reason for a separate API, such as:

- Multiple independent clients.
- Independent frontend and agent\-service scaling.
- A required HTTP contract demonstrated by the source repository.
- Long\-running work that must continue independently of a Blazor circuit.
- A meaningful security or deployment boundary.
- A deliberate educational demonstration of distributed application composition.

If a separate API is retained, document the reason in an Architecture Decision Record. Use typed HttpClient clients, health endpoints, resilience policies, distributed tracing, and an explicitly selected streaming protocol.

Do not create empty architectural layers or projects that provide no demonstrated value.

Use .sln unless .slnx provides a verified benefit and is supported by all required developer, CI/CD, and deployment tooling.

---

## 6\. Blazor Frontend

Implement the frontend using the .NET 10 Blazor Web App template.

### 6.1 Render mode

Default to Interactive Server render mode.

This keeps:

- Azure SDK calls.
- Azure AI operations.
- Azure AI Search calls.
- Storage operations.
- Managed\-identity authentication.
- Agent orchestration.
- Privileged configuration.

on the server.

Use Interactive Auto only for components with a documented client\-execution requirement. Components using Interactive Auto must not contain:

- Managed\-identity operations.
- Server\-only SDK dependencies.
- Privileged configuration.
- Secrets.
- Direct calls to protected Azure data\-plane services.
- Logic that assumes access to server\-side resources.

Document any decision to use Interactive Auto.

### 6.2 Component design

Use a clear component structure with:

- Page components.
- Reusable presentation components.
- Feature\-specific components.
- Application services.
- Typed view models or state models.
- Explicit loading, empty, error, cancellation, and completion states.

Avoid placing agent orchestration, Azure SDK calls, or infrastructure logic directly inside Razor components.

### 6.3 Streaming responses

Represent streamed agent output using a typed asynchronous stream such as:

IAsyncEnumerable<AgentStreamEvent>

Model distinct event types where required, including:

- Text delta.
- Completed text.
- Tool\-call started.
- Tool\-call completed.
- Citation or grounding result.
- Status update.
- Usage information.
- Warning.
- Error.
- Completion.

Propagate CancellationToken from the UI to the underlying agent or model operation.

The UI must support:

- Incremental rendering.
- User cancellation.
- Partial output.
- Tool\-call progress.
- Timeout handling.
- Recoverable and non\-recoverable errors.
- Connection interruption.
- Blazor circuit reconnect behaviour.
- Accessible status announcements.
- Prevention of duplicate submissions.

If the agent capability is exposed through a separate API, use an explicitly selected streaming protocol such as Server\-Sent Events. Do not assume that IAsyncEnumerable<T> alone defines the external transport.

Do not automatically retry partially completed generative operations unless the retry semantics are safe and clearly communicated to the user.

### 6.4 Styling and accessibility

Use Fluent UI Blazor only after confirming that its selected version supports .NET 10 and the chosen render mode.

Otherwise, use maintainable CSS and standard Blazor components.

The UI must provide:

- Responsive layout.
- Keyboard navigation.
- Visible focus states.
- Semantic HTML.
- Appropriate ARIA attributes.
- Sufficient colour contrast.
- Screen\-reader announcements for streamed state changes and errors.

Do not make the application dependent on a UI package when standard Blazor components are sufficient.

---

## 7\. AI and Agent SDK Selection

Use current supported Microsoft .NET SDKs. Select the minimum appropriate abstraction for each migrated scenario.

Do not require every AI request to pass through every available SDK.

### 7.1 Microsoft.Extensions.AI

Prefer Microsoft.Extensions.AI when the scenario benefits from:

- IChatClient or related portable abstractions.
- Dependency injection.
- Provider abstraction.
- Middleware.
- Telemetry.
- Caching.
- Test doubles.
- Common chat message and response models.

### 7.2 Semantic Kernel

Use Semantic Kernel only for demonstrations that require capabilities such as:

- Plugins.
- Kernel functions.
- Function invocation.
- Agent abstractions.
- Process orchestration.
- Memory abstractions.
- Semantic Kernel\-specific filters or middleware.
- Other Semantic Kernel features explicitly demonstrated by the source.

Do not add Semantic Kernel solely as a wrapper around another SDK if it provides no functional or instructional value.

### 7.3 Azure AI Foundry SDK

Use the current supported Azure AI Foundry .NET SDK for Foundry\-specific capabilities such as:

- Foundry account or project operations.
- Managed agents.
- Project connections.
- Model deployments.
- Evaluations.
- Foundry\-specific data or tool integrations.
- Other capabilities demonstrated by the source repository.

Before implementation, verify the current GA package names, namespaces, API versions, support status, and regional availability.

Do not assume that older Azure AI Projects SDK examples remain current.

### 7.4 Azure AI Search

Use Azure.Search.Documents for Azure AI Search operations, including:

- Search queries.
- Index management.
- Document ingestion.
- Vector search.
- Semantic ranking.
- Hybrid retrieval.

Separate query\-time permissions from index\-management and ingestion permissions.

### 7.5 Other Azure SDKs

Use current Azure SDK libraries for required services, such as:

- Azure.Identity.
- Azure.Storage.Blobs.
- Azure Monitor and OpenTelemetry integrations.
- Other service\-specific SDKs identified by the source audit.

### 7.6 SDK decision record

Produce an SDK selection table containing:

| Source capability | Proposed .NET SDK | Abstraction used | Package status | Reason selected | Rejected alternative |
| --- | --- | --- | --- | --- | --- |

Record any use of a preview package in an Architecture Decision Record. Include:

- Why the preview package is necessary.
- The affected functionality.
- The upgrade or replacement path.
- The risk to repository stability.

---

## 8\. Authentication and Managed Identity

Use keyless Azure authentication.

Do not store or depend on:

- Azure client secrets.
- Service API keys.
- ACR administrator credentials.
- Storage account keys.
- Search administration keys.
- Foundry or model API keys.
- Connection strings containing secrets.
- Long\-lived GitHub Actions Azure credentials.

### 8.1 Local development

Use DefaultAzureCredential for local development.

Support authenticated developer tooling such as:

- Azure CLI.
- Visual Studio.
- Visual Studio Code.
- Other supported Azure developer credentials.

Document the required local Azure sign\-in and RBAC assignments.

The application must fail with a clear diagnostic message when no suitable local credential is available.

### 8.2 Azure runtime

In Azure Container Apps:

- Assign the selected user\-assigned managed identity explicitly.
- Configure the identity client ID explicitly where required.
- Use deterministic managed\-identity authentication.
- Prevent production authentication from silently falling through to developer, interactive, CLI, or environment\-secret credentials.
- Keep authentication configuration consistent across services.

Use ManagedIdentityCredential directly or constrain DefaultAzureCredential to the intended managed identity. Document the selected approach.

### 8.3 Identity separation

Use separate identities or principals for these responsibilities unless the audit provides a documented reason to combine them:

1. Application runtime identity.
2. Container Apps image\-pull identity.
3. GitHub Actions deployment identity.
4. Search indexing or ingestion identity, if indexing requires privileges beyond runtime query access.

The application runtime identity must not receive deployment permissions.

The image\-pull identity should normally receive only AcrPull on the target Azure Container Registry.

The GitHub deployment identity must not be reused as the application runtime identity.

---

## 9\. Azure RBAC and Least Privilege

Produce an explicit RBAC matrix before implementing role assignments.

The matrix must include:

| Principal | Azure resource | Scope | Role | Required operation | Runtime or deployment | Justification |
| --- | --- | --- | --- | --- | --- | --- |

Consider roles such as:

- Cognitive Services OpenAI User.
- Azure AI Developer.
- Search Index Data Reader.
- Search Index Data Contributor.
- Search Service Contributor.
- Storage Blob Data Reader.
- Storage Blob Data Contributor.
- AcrPull.
- AcrPush.
- Monitoring\-related roles.
- Resource deployment roles required by the GitHub identity.

Do not assign all listed roles automatically.

Determine the minimum role from actual operations:

- Model inference should receive inference permissions, not broad resource\-management permission.
- Search query operations should receive data\-reader permission.
- Index creation and ingestion must use a separate role or identity when elevated permissions are required.
- Read\-only Blob access should use Storage Blob Data Reader.
- Blob write or delete capability should use Storage Blob Data Contributor only when required.
- AcrPull should be assigned to the identity that Azure Container Apps uses to pull images.
- AcrPush should normally be limited to the CI/CD deployment principal.
- Azure AI Developer must not be assigned to the runtime identity unless the application genuinely performs the corresponding runtime operations.

Apply role assignments at the narrowest practical scope.

Avoid subscription\-level role assignments unless a documented deployment requirement makes them necessary.

Use deterministic GUID generation for Bicep role\-assignment names.

---

## 10\. Azure Infrastructure

Use Bicep under infra/ as the authoritative definition of production Azure infrastructure.

Use azd to coordinate provisioning and deployment.

Do not create a second competing production infrastructure definition through .NET Aspire.

### 10.1 Required capability areas

Provision only the resources required by:

- A mapped source capability.
- An explicit non\-functional requirement.
- A documented platform dependency.
- A deliberate educational demonstration.

The expected topology may include:

#### AI and retrieval

- The current supported Azure AI Foundry account or hub resource required by the selected architecture.
- The current supported Foundry project resource.
- Required model deployments.
- Azure AI Search using Microsoft.Search/searchServices.
- Foundry IQ or other retrieval capabilities only where explicitly required.

Do not treat Foundry IQ and Azure AI Search as synonymous ARM resources. Model their relationship according to the current supported Azure architecture and the migrated scenario.

#### Compute and containers

- Azure Container Registry.
- Azure Container Apps managed environment.
- One or more Azure Container Apps.
- Managed identities for runtime and image pull.
- Optional Container Apps jobs if required for indexing or background processing.

#### Storage

- Azure Storage account.
- Blob containers or other storage resources required by the application.
- Private containers by default.
- Public access disabled unless a documented scenario requires it.

#### Observability

- Log Analytics workspace.
- Application Insights workspace\-based resource.
- Diagnostic settings where required.
- Alert rules based on defined operational requirements.
- Smart Detection or failure anomaly alerting only where supported and justified.

#### Security and configuration

- User\-assigned managed identities.
- Azure RBAC role assignments.
- Key Vault only if the application has secrets or certificates that cannot be eliminated through managed identity.
- Managed configuration services only where their use is justified.

Do not provision Key Vault merely to store values that can be represented as non\-secret environment configuration.

### 10.2 Resource parity rule

Do not reproduce every resource found in an existing resource group merely to achieve visual topology parity.

For every resource in the source topology, classify it as:

- Required for feature parity.
- Required for a non\-functional requirement.
- Required by the Azure platform.
- Optional educational enhancement.
- Existing but unused.
- Replaced by a different target capability.
- Omitted with justification.

Document omitted source resources and newly introduced resources.

---

## 11\. Azure Region and Model Availability

Default regional resources to:

australiaeast

Use separate Bicep parameters for:

- primaryLocation
- aiLocation

Default both parameters to australiaeast where supported.

Do not silently deploy AI resources or model deployments into another region.

If a required model, SKU, feature, or API is unavailable in Australia East:

1. Detect or document the limitation during pre\-deployment validation.
2. Require the fallback aiLocation to be supplied explicitly.
3. Fail deployment with a clear message when no supported location has been selected.
4. Document the reason for the alternate region.
5. Document data residency, latency, compliance, cost, and networking implications.
6. Keep all other eligible resources in Australia East unless another dependency prevents it.

Do not implement runtime region fallback.

The infrastructure must use the region selected at deployment time.

Model names, versions, deployment types, and quotas must be configurable rather than embedded in application code.

---

## 12\. Bicep Standards

Structure Bicep into focused modules, such as:

- Managed identities.
- Azure Container Registry.
- Log Analytics.
- Application Insights.
- Container Apps environment.
- Container Apps.
- Storage.
- Azure AI Foundry resources.
- Model deployments.
- Azure AI Search.
- Role assignments.
- Monitoring and alerts.

Use:

- Current supported API versions.
- Parameters for environment\-specific values.
- Consistent naming.
- Azure resource tags.
- Secure parameters only when genuinely required.
- Resource outputs that are safe to expose.
- Existing\-resource references where appropriate.
- Deterministic deployment behaviour.
- Bicep linting.
- Deployment validation before execution.

Do not output secrets, account keys, connection strings, or deployment credentials.

Use Azure Verified Modules where they provide a suitable, supported abstraction and do not obscure the educational purpose of the repository. Otherwise, use focused custom Bicep modules.

---

## 13\. .NET Aspire

Use .NET Aspire for local developer orchestration.

The AppHost should compose the required application projects and local dependencies.

Use ServiceDefaults for shared application defaults such as:

- OpenTelemetry.
- Health checks.
- Service discovery.
- HTTP resilience.
- Common logging and telemetry configuration.

Aspire may model references to Azure services and local development substitutes, but it must not become a second independent source of truth for production infrastructure.

The local developer workflow should support:

- Starting the complete application with one command.
- Viewing logs and traces in the Aspire dashboard.
- Discovering service endpoints.
- Running with local developer credentials.
- Replacing or emulating dependencies where practical.
- Clear diagnostics when required Azure resources or credentials are unavailable.

Do not use docker\-compose.yaml.

Do not add Docker Compose indirectly through generated tooling or documentation.

---

## 14\. Containerization

All deployable application services must be packaged as OCI\-compatible container images.

Choose one container\-build approach per deployable project:

- .NET SDK container publishing, or
- A reviewed multi\-stage Dockerfile.

Do not maintain two competing container\-build paths for the same service without a documented reason.

Container images must:

- Use supported .NET 10 runtime images.
- Run as a non\-root user where supported.
- Expose only required ports.
- Avoid including SDK tooling in the final runtime layer when using Dockerfiles.
- Avoid copying source files, test assets, local configuration, or secrets into runtime images.
- Include health endpoints appropriate for Azure Container Apps.
- Use deterministic tags such as commit SHA and application version.
- Avoid relying only on the latest tag.
- Be scanned for known vulnerabilities.
- Be reproducibly buildable in CI.

Container Apps must use:

- Managed identity for ACR image pulls.
- Ingress only where required.
- Appropriate revision mode.
- Configurable minimum and maximum replicas.
- Defined CPU and memory allocations.
- Liveness and readiness probes.
- Secure environment variables.
- Secret references only when a secret cannot be eliminated.
- HTTPS for external ingress.
- Appropriate scaling rules based on the application workload.

Document the consequences of scaling Interactive Server Blazor across multiple replicas, including SignalR connections, session affinity where applicable, reconnection, per\-circuit memory, and load testing.

---

## 15\. Application Architecture

Use clear separation of concerns without overengineering.

A typical dependency direction should be:

- UI or API depends on Application.
- Application depends on Domain abstractions.
- Infrastructure implements Application or Domain interfaces.
- Domain does not depend on Azure SDKs, ASP.NET Core, Blazor, or infrastructure projects.

Use interfaces where they provide:

- Testability.
- Provider isolation.
- A meaningful architectural boundary.
- Support for multiple implementations.

Do not create interfaces for every class automatically.

Keep Azure SDK types near the infrastructure boundary. Map them to application\-owned models where doing so reduces coupling or improves testability.

Use strongly typed configuration classes and validate required options at startup.

Do not allow the application to start successfully with invalid required configuration.

---

## 16\. Reliability and Resilience

Use resilience policies appropriate to each operation.

Apply:

- Timeouts.
- Cancellation.
- Bounded retries for safe transient failures.
- Exponential backoff with jitter.
- Rate\-limit handling.
- Circuit breaking where appropriate.
- Clear error classification.
- Idempotency for repeatable write operations.
- Request\-size and response\-size limits where appropriate.

Do not automatically retry:

- Partially completed streamed model responses.
- Non\-idempotent tool operations.
- Operations where retry could duplicate external side effects.
- Authentication or authorization failures.

Expose user\-safe errors through the UI while preserving detailed diagnostic information in structured server logs.

Do not disclose:

- Tokens.
- Credentials.
- Internal endpoints.
- Stack traces.
- Prompt contents.
- Sensitive retrieved documents.
- Personal information.

---

## 17\. Observability

Provisioning Application Insights is not sufficient by itself. Instrument the application.

Use OpenTelemetry for:

- Distributed traces.
- Metrics.
- Structured logs.
- Correlation between the Blazor frontend, Agent API if present, AI operations, tools, Search, and Storage.
- Dependency telemetry.
- Exception telemetry.

Capture useful measurements where available, including:

- End\-to\-end request duration.
- Model response latency.
- Time to first streamed token or event.
- Total streamed response duration.
- Model or agent failure count.
- Tool\-call duration and failure count.
- Search query latency.
- Number of search results.
- Token usage where exposed by the provider.
- Throttling and quota errors.
- User cancellation.
- Blazor circuit connection and failure metrics.
- Container restart and health\-probe failures.

Do not capture prompts, generated responses, retrieved documents, access tokens, secrets, or personal information by default.

If development\-time content logging is provided:

- Disable it by default.
- Make it explicitly configurable.
- Clearly warn that it may contain sensitive data.
- Ensure it cannot be enabled accidentally in production.

Add useful Application Insights queries and operational guidance to the documentation.

Create alert rules only for defined operational signals, such as:

- Sustained application failure rate.
- Container restart loops.
- Readiness probe failures.
- High dependency failure rate.
- Model throttling.
- Excessive response latency.

---

## 18\. Testing Strategy

Use a balanced test suite.

### 18.1 Unit tests

Use xUnit unless the source repository provides a strong reason to select NUnit.

Unit\-test:

- Application services.
- Agent orchestration logic.
- Event mapping.
- Tool dispatch.
- Validation.
- Configuration mapping.
- Error classification.
- Retry decisions.
- Domain behaviour.
- Security\-sensitive decisions.

Use mocked or fake AI clients rather than calling live models in unit tests.

### 18.2 Blazor component tests

Use bUnit for:

- Rendering.
- User interactions.
- Streaming state updates.
- Cancellation.
- Empty state.
- Loading state.
- Error state.
- Tool\-call presentation.
- Accessibility\-relevant behaviour.

### 18.3 Integration tests

Use WebApplicationFactory for ASP.NET Core integration tests where applicable.

Test:

- Dependency injection.
- Configuration validation.
- Authentication and authorization behaviour.
- HTTP contracts.
- Streaming endpoints.
- Health endpoints.
- Error responses.
- Telemetry integration where practical.

Do not require live Azure services for the default local test run.

Separate tests that require deployed Azure resources into an explicitly named test category or workflow.

### 18.4 End\-to\-end tests

Use Playwright for a focused set of critical user journeys, such as:

- Opening the application.
- Submitting an agent request.
- Receiving streamed output.
- Cancelling an active request.
- Viewing a handled failure.
- Confirming the main accessible interaction path.

### 18.5 Infrastructure and deployment tests

Validate:

- Bicep formatting and linting.
- Bicep template compilation.
- Deployment validation.
- Required parameter handling.
- Container image build.
- Non\-root execution where configured.
- Container startup.
- Liveness endpoint.
- Readiness endpoint.
- Azure Container Apps deployment.
- Managed\-identity authentication.
- Required RBAC access.
- Expected denial when a principal lacks permission.

---

## 19\. CI/CD with GitHub Actions

Port or replace the source workflows with GitHub Actions appropriate to the C\#/.NET solution.

Use GitHub Actions OpenID Connect workload identity federation for Azure authentication.

Do not use a stored Azure service\-principal client secret.

Use separate validation and deployment responsibilities.

### 19.1 Pull request validation

The pull\-request workflow must perform, as applicable:

1. Restore dependencies.
2. Verify dependency lock state.
3. Build in Release configuration.
4. Run unit tests.
5. Run component tests.
6. Run integration tests that do not require live Azure resources.
7. Run dotnet format \-\-verify\-no\-changes.
8. Compile and lint Bicep.
9. Build container images.
10. Run container startup and health smoke tests.
11. Scan dependencies and container images.
12. Scan the repository for prohibited Python artifacts.
13. Publish test results and useful diagnostics.

### 19.2 Deployment workflow

The deployment workflow must:

1. Authenticate to Azure using OIDC.
2. Build immutable container images.
3. Tag images with commit SHA and, where appropriate, semantic version.
4. Push images to ACR using the deployment principal.
5. Validate the Bicep deployment.
6. Provision or update infrastructure through azd and Bicep.
7. Deploy image references without rebuilding already validated artifacts.
8. Wait for the new Container Apps revision to become healthy.
9. Run post\-deployment smoke tests.
10. Report the deployed revision and endpoints.
11. Fail clearly and preserve diagnostic information if deployment verification fails.

Use GitHub Environments and required approvals for protected environments where appropriate.

Avoid granting the GitHub identity broad subscription\-wide ownership.

---

## 20\. Strict Zero\-Python Requirement

The final repository must contain no Python runtime dependency.

Do not copy, generate, or retain:

- .py files.
- requirements.txt.
- pyproject.toml.
- Pipfile.
- Pipfile.lock.
- poetry.lock.
- uv.lock.
- Conda environment files.
- .venv configuration.
- Python dev container features.
- Python base images.
- Python runtime layers.
- Python\-based GitHub Actions steps.
- Python deployment scripts.
- Python test frameworks.
- Python package installation.
- Documentation that instructs users to install or execute Python.

Use:

- C\# and .NET for application and automation logic.
- Bicep for Azure infrastructure.
- YAML for GitHub Actions and azd.
- JSON where required.
- PowerShell or Bash only where shell scripting is genuinely necessary.

Documentation may mention source Python filenames only in:

- The migration mapping.
- Historical architecture explanations.
- A record of components that were replaced.

Add an automated CI check that fails if prohibited Python files, manifests, runtimes, container layers, or CI steps are found.

Do not fail the check merely because a migration document contains the name of a historical Python source file.

---

## 21\. Configuration and Secrets

Use the standard .NET configuration hierarchy.

Support configuration through:

- appsettings.json for safe defaults.
- appsettings.Development.json for safe development defaults.
- Environment variables.
- Aspire resource references for local composition.
- Azure Container Apps environment variables and secret references where necessary.

Do not commit real service endpoints if they are environment\-specific and sensitive.

Do not commit:

- API keys.
- Client secrets.
- Storage keys.
- Search keys.
- Registry passwords.
- Access tokens.
- Private certificates.
- Production connection strings.

Include .env files in .gitignore if local shell tooling requires them, but do not make .env files the primary configuration mechanism for the .NET application.

Provide a safe example configuration file where useful.

Validate required configuration at startup and return actionable errors.

---

## 22\. Security Requirements

Apply secure defaults throughout the application and infrastructure.

Include:

- Managed identity.
- Least\-privilege RBAC.
- HTTPS.
- Private storage containers.
- Disabled public Blob access.
- Secure HTTP headers where applicable.
- Request\-size limits.
- Input validation.
- Output encoding.
- Protection against prompt injection where tools or retrieved data can influence privileged operations.
- Explicit tool allowlists.
- Validation and authorization for tool arguments.
- Clear separation between user content, system instructions, retrieved content, and tool results.
- No secrets in logs.
- No sensitive prompt or response recording by default.
- Dependency and image vulnerability scanning.
- Regular base\-image update capability.
- Restricted CORS when an API is present.
- Authentication and authorization for non\-public application endpoints.

Agent tools must not execute arbitrary commands, arbitrary SQL, or unrestricted HTTP requests unless the source scenario explicitly requires such behaviour and an appropriate sandbox and allowlist are implemented.

Treat retrieved documents and tool output as untrusted input.

---

## 23\. Documentation

Rewrite the documentation for C\# and Azure. Do not mechanically edit the Python instructions.

The root README.md must include:

- Repository purpose.
- Migrated demonstrations.
- Architecture overview.
- Solution structure.
- Prerequisites.
- Required .NET SDK version.
- Required Azure CLI and azd versions or minimum supported versions.
- Local Azure authentication.
- Required Azure permissions.
- Local startup using Aspire.
- Running tests.
- Building containers.
- Deploying with azd.
- Region and model availability configuration.
- Managed\-identity behaviour.
- Troubleshooting.
- Cleanup and resource deletion.
- Cost and quota considerations.
- Known limitations.
- Confirmation that Python is not required.

Add focused documentation for:

- Source\-to\-target migration mapping.
- Architecture decisions.
- Azure topology.
- Identity and RBAC.
- Local development.
- Container build and deployment.
- GitHub Actions OIDC setup.
- Observability.
- Testing.
- Security.
- Regional fallback.
- Troubleshooting.

Use Mermaid diagrams where suitable for:

- Logical application architecture.
- Local Aspire topology.
- Azure deployment topology.
- Authentication flows.
- Streaming request flow.
- CI/CD workflow.

Ensure every documented command is valid for the final repository.

---

## 24\. Architecture Decision Records

Create ADRs for material decisions, including:

- Blazor Interactive Server versus Interactive Auto.
- Modular monolith versus separate Agent API.
- Microsoft.Extensions.AI versus direct provider SDK use.
- Semantic Kernel usage.
- Azure AI Foundry SDK selection.
- Aspire scope.
- Bicep as the production infrastructure source of truth.
- .NET SDK container publishing versus Dockerfiles.
- Runtime identity separation.
- Search ingestion identity and architecture.
- Australia East model availability and fallback.
- Use of any preview SDK, API, or Azure resource type.

Each ADR must include:

- Context.
- Decision.
- Alternatives considered.
- Consequences.
- Risks.
- Follow\-up actions.

---

## 25\. Implementation Sequence

Execute the migration in the following order.

### Phase 1: Audit and design

- Inventory the source repository.
- Produce the migration mapping.
- Produce the feature\-parity matrix.
- Identify Azure dependencies.
- Confirm current Azure resource types and API versions.
- Confirm model and SKU availability in Australia East.
- Select the minimum required SDKs.
- Define the target application boundaries.
- Produce the identity and RBAC matrix.
- Create initial ADRs.
- Identify unsupported or ambiguous source capabilities.

### Phase 2: Scaffold

- Create the .NET solution.
- Add central package and build configuration.
- Add Application, Domain, Infrastructure, and Web projects as justified.
- Add the Agent API only if justified.
- Add Aspire AppHost and ServiceDefaults.
- Add test projects.
- Add Bicep and azd structure.
- Add initial CI validation.
- Add zero\-Python enforcement.

### Phase 3: Core implementation

- Port domain and application capabilities.
- Implement AI provider abstractions.
- Implement agent demonstrations.
- Implement tool calling.
- Implement retrieval and indexing where required.
- Implement configuration validation.
- Implement authentication.
- Implement typed streaming event models.
- Add unit and integration tests.

### Phase 4: Blazor implementation

- Implement the interactive agent experience.
- Add streaming updates.
- Add cancellation.
- Add tool progress.
- Add error and reconnect handling.
- Add accessibility behaviour.
- Add bUnit tests.
- Add focused Playwright tests.

### Phase 5: Azure infrastructure

- Implement modular Bicep.
- Implement Foundry and model resources using current supported resource types.
- Implement Azure AI Search.
- Implement Storage.
- Implement ACR.
- Implement the Container Apps environment and applications.
- Implement managed identities.
- Implement scoped RBAC assignments.
- Implement observability.
- Validate Australia East deployment and explicit AI\-region fallback.

### Phase 6: Containers and deployment

- Standardize the container build method.
- Build immutable images.
- Run startup and health checks.
- Configure ACR managed\-identity pulls.
- Implement azd deployment.
- Implement GitHub Actions OIDC deployment.
- Add post\-deployment verification.

### Phase 7: Documentation and final verification

- Rewrite the documentation.
- Complete ADRs.
- Verify every documented command.
- Run all tests and quality checks.
- Scan for prohibited Python artifacts.
- Deploy to a clean Azure environment.
- Run smoke tests.
- Confirm managed\-identity access.
- Confirm least\-privilege denial behaviour.
- Record any remaining parity gaps.

---

## 26\. Definition of Done

The migration is complete only when all applicable criteria below are satisfied.

### Repository and code

- The .NET 10 solution restores successfully.
- dotnet build \-\-configuration Release succeeds.
- Repository\-owned code produces no unapproved warnings.
- dotnet test \-\-configuration Release succeeds.
- dotnet format \-\-verify\-no\-changes succeeds.
- Nullable reference types are enabled.
- Required analyzers are enabled.
- Package versions are centrally managed.
- No secret is committed.
- No prohibited Python artifact remains.

### Functional parity

- Every source demonstration is classified in the parity matrix.
- Every migrated demonstration works through the C\# implementation.
- Any intentionally omitted capability has documented justification.
- Agent responses can be streamed to the Blazor UI.
- Cancellation works.
- Tool calls are displayed and handled correctly.
- Retrieval works where required.
- Errors are handled without exposing sensitive details.

### Azure

- Bicep compiles and passes linting.
- The deployment succeeds in Australia East where services and model SKUs are available.
- Any alternate AI region must be explicitly selected.
- Azure resource types and API versions are current and supported.
- Container images are stored in ACR.
- Azure Container Apps pulls images using managed identity.
- Runtime access uses managed identity.
- RBAC is scoped according to the approved matrix.
- Liveness and readiness probes pass.
- Application Insights receives traces, metrics, dependencies, and exceptions.
- Sensitive prompt or response content is not captured by default.

### Containers

- Every deployable service builds as an OCI image.
- Runtime images contain no Python runtime or tooling.
- Runtime images contain no build\-time SDK unless explicitly justified.
- Containers run as non\-root where supported.
- Containers start successfully using production\-like configuration.
- Container vulnerability scanning completes.
- Immutable image tags are used for deployment.

### CI/CD

- Pull requests run build, formatting, tests, Bicep validation, container build, security scanning, and zero\-Python checks.
- GitHub deployment uses OIDC.
- No long\-lived Azure client secret is used.
- Deployment uses immutable images.
- Post\-deployment smoke tests pass.
- Protected environments use approvals where configured.

### Documentation

- README instructions work from a clean development environment.
- Local Aspire startup is documented.
- Azure deployment is documented.
- Identity and RBAC are documented.
- Australia East and fallback\-region behaviour are documented.
- Architecture diagrams reflect the final implementation.
- Troubleshooting guidance covers common authentication, quota, model\-availability, RBAC, streaming, and deployment failures.
- Azure cleanup instructions are included.

---

## 27\. Required First Response

Before writing implementation code, return:

1. An executive summary of the proposed migration.
2. The source repository inventory.
3. The feature\-parity matrix.
4. The file\-by\-file migration table.
5. The proposed target solution structure.
6. The proposed SDK selection matrix.
7. The proposed Azure resource mapping.
8. The identity and RBAC matrix.
9. The proposed Australia East deployment strategy.
10. The proposed test strategy.
11. The proposed CI/CD workflow.
12. The initial ADR list.
13. Material risks, ambiguities, and unsupported assumptions.
14. The phased implementation plan.
15. A list of decisions requiring confirmation before implementation.

Do not write the complete implementation in the first response.

Where the source repository or current Azure documentation does not provide enough information, state the uncertainty explicitly. Do not invent resource types, SDK APIs, package names, model availability, role assignments, or source behaviours.

After the migration plan is approved, implement the work phase by phase and verify each phase before proceeding.