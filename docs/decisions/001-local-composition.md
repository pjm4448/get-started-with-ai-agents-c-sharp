# Local composition and shared service defaults

## Context

The migration requires Aspire for local development and Bicep as the sole
authoritative production infrastructure definition. The web application still
refuses startup while authentication and agent integration are incomplete.

## Decision

Use the AppHost solely to compose local application projects. It does not
provision Azure services or generate the production infrastructure.

Use ServiceDefaults for shared host behavior:

- `/alive` runs only the process liveness check.
- `/health` runs all registered checks, including dependency readiness checks.
- Both probe endpoints are anonymous and expose only health status, not
  dependency details. Production ingress rules must account for this.
- HttpClient receives service discovery and standard resilience policies.
  Retries are disabled for unsafe methods, including generative POST requests.
  Agent SDK retry policies must be configured separately; these defaults do
  not control Azure SDK transports or make replaying partial responses safe.
- Trace and metric export is enabled only when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is configured, as by Aspire.
- HTTP instrumentation does not record exception details. No body, prompt,
  response-content, or log exporter is configured here.

The web project references ServiceDefaults, but startup does not invoke it yet.
The helper library is not a claim that the web app, dashboard, sign-in, or
Azure telemetry export has been verified end to end.

## Alternatives considered

- Docker Compose: excluded by the migration requirements.
- Aspire-generated production infrastructure: would compete with Bicep.
- Retry all HTTP methods: risks repeating side effects and model operations.
- Apply all shared configuration directly in the web project: obscures the
  reusable host boundary required by the local composition design.

## Consequences

Readiness failures can be reported independently of process liveness.
Default tests need no Azure services. Exporting telemetry remains explicitly
configured rather than a prerequisite for startup.

## Risks

URLs, route values, and telemetry attributes may still contain sensitive
identifiers. HTTP exception suppression alone is not a full telemetry privacy
policy. Do not place prompts, document contents, or tokens in URLs or activity
tags. Production signal export and redaction require further verification.

The first local AppHost launch failed while extracting the Aspire CLI bundle
in the package cache. Building the resource graph succeeded, but the
dashboard and composed application have not been verified.

## Follow-up actions

Wire these helpers into authenticated application startup, test the actual
probe HTTP contracts, verify privacy with exported telemetry, and configure
Azure Monitor through Bicep and managed identity. Define dependency readiness
checks from actual runtime requirements rather than adding paid inference
calls to health probes.
