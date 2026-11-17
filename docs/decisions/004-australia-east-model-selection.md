# Australia East deployment and model selection

## Context

The user selected preparation of a new Foundry project in Australia East under
the previously confirmed tenant and subscription. This is not authorization
to provision resources. The migration requires separate primary and AI
locations, defaulting both to `australiaeast`, and forbids silent fallback.

Read-only regional catalog and subscription usage queries on 2026-11-17 found:

- `gpt-5-mini` version `2025-08-07` advertised for AIServices S0 in Australia
  East, with Responses and agentsV2 capabilities. The pay-per-token deployment
  SKU is GlobalStandard; regional Standard was not advertised for this model.
- The GlobalStandard quota record reports 1000 capacity units, 80 used and 920
  remaining. This is not a guarantee of allocatable capacity or live success.
- `text-embedding-3-small` version `1` advertises regional Standard, with a
  quota record of 350 capacity units, none used. It is a candidate for optional
  Azure AI Search ingestion, not a mandatory extra deployment for File Search.

## Decision

Keep both proposed locations in Australia East. Do not create an alternate
region, runtime fallback or deployment until explicitly approved.

Use the source's gpt-5-mini/version combination as a **candidate**, not an
approved deployment default. Resolve whether global inference processing is
acceptable before selecting GlobalStandard. If Australia-only inference is
required, discover a different supported regional model and record its parity,
quality, cost and quota implications before implementation.

Do not silently substitute a model to avoid a quota or residency constraint.
Keep deployment model, version, SKU and capacity configurable.

## Alternatives considered

- GlobalStandard with Australia East resource placement: aligns with the source
  model and has reported quota, but does not guarantee Australia-only inference.
- A different model using regional Standard: may better fit residency needs;
  exact model support and agent/retrieval compatibility remain unverified.
- An alternate AI region: requires explicit user selection and documented
  residency, latency, networking, compliance and cost consequences.
- Provisioned throughput: introduces a commitment that is not justified for
  this educational development workload and has not been approved.

## Consequences

No region fallback is currently needed on the available catalog evidence, but
deployment readiness is not established. The final model/SKU remains gated on
residency acceptance, cost review and fresh capacity verification.

## Risks

Catalog availability and quota do not prove actual project creation,
retrieval-tool compatibility, deployment capacity or successful inference.
The catalog lists gpt-5-mini inference deprecation on 2027-07-13, so its upgrade
path must be revisited before production.

The source Bicep's gpt-5-mini default version differs from its parameter file.
Only the latter's 2025-08-07 version matched the observed catalog entry.

## Follow-up actions

Confirm processing residency requirements and estimated usage. Validate the
remaining service APIs, scoped RBAC, Search support, resource costs and source
audit gaps. Recheck catalog/quota immediately before an explicitly approved
Bicep/azd deployment. Keep infrastructure changes in the migration's existing
Container Apps topology rather than introducing a competing hosted-agent
infrastructure definition.

## References

- [Regional model catalog CLI](https://learn.microsoft.com/cli/azure/cognitiveservices/model#az-cognitiveservices-model-list)
- [OpenAI deployment types and processing geography](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/deployment-types)
