# Deployment region and model selection

## Context

The initial migration baseline defaulted both primary and AI locations to
`australiaeast`. An Australia-only processing requirement then prompted a
regional Standard model investigation. Both are historical context, not the
currently selected deployment policy.

On 2026-11-17 the user explicitly revised the choice to **East US, gpt-5-mini,
Global Standard**, confirmed acceptance of global inference processing, and
selected East US for future non-AI regional resources too. This supersedes the
Australia-only requirement. The tenant/subscription and local-first text-only
milestone are unchanged. Retrieval and cloud hosting remain later phases.
No budget amount or provisioning has been approved.

## Current read-only evidence

East US catalog and subscription usage queries on 2026-11-17 found:

- AIServices S0 advertises gpt-5-mini version `2025-08-07` as
  GenerallyAvailable, with Responses and agentsV2 capabilities and a
  GlobalStandard SKU.
- The exact `OpenAI.GlobalStandard.gpt-5-mini` record reports a limit of 1000,
  80 used and **920 remaining**, unit Count. These are service-reported
  capacity counts, not measured throughput or a deployment guarantee.
- The catalog reports inference/SKU deprecation on 2027-07-13.
- Default capacity is 10, with minimum and step unspecified. Deployment
  capacity is not yet selected.

## Historical Australia East evidence

Earlier read-only queries on the same date found:

- `gpt-5-mini` version `2025-08-07` advertised for AIServices S0 in Australia
  East, with Responses and agentsV2 capabilities. The pay-per-token deployment
  SKU is GlobalStandard; regional Standard was not advertised for this model.
- The GlobalStandard quota record reports 1000 capacity units, 80 used and 920
  remaining. This is not a guarantee of allocatable capacity or live success.
- `text-embedding-3-small` version `1` advertises regional Standard, with a
  quota record of 350 capacity units, none used. It is a candidate for optional
  Azure AI Search ingestion, not a mandatory extra deployment for File Search.

Further read-only AIServices S0 catalog and usage queries on the same date
identified the following Standard candidates, all advertising Responses and
agentsV2:

| Model | Version | Lifecycle / inference and SKU deprecation | Quota limit / used / remaining |
| --- | --- | --- | --- |
| gpt-5.6-sol | 2026-07-09 | GenerallyAvailable / 2028-01-11 | **0 / 0 / 0** |
| gpt-4.1-mini | 2025-04-14 | Legacy / 2027-04-14 | **0 / 0 / 0** |
| gpt-4o | 2024-11-20 | Legacy / 2027-04-14 | **0 / 0 / 0** |

These historical quotas are not the current East US allocation.
The exact quota record for gpt-4.1-mini is
`OpenAI.Standard.gpt4.1-mini`; do not infer usage identifiers from model names.
See the [deployment proposal](../../.azure/deployment-plan.md) for all record
names, API evidence, pricing limitations and approval gates.

## Decision

Use explicit `primaryLocation=eastus` and `aiLocation=eastus` selections for
this environment. This is an approved exception to the original Australia
East baseline, not an automatic fallback. Future Container Apps, ACR,
retrieval and monitoring regional resources should use East US where
supported; none has been created or moved.

Use gpt-5-mini and the `GlobalStandard` deployment type in the proposal.
Version `2025-08-07` is the currently verified catalog candidate; capacity,
final deployment parameters and spending remain subject to review.

GlobalStandard can process inference globally, not only in East US or the
United States. The user explicitly accepted this. Foundry state storage must
be reviewed against the US resource geography; do not claim Australian
residency or East US-only processing. Review content safety, abuse monitoring,
retention, deletion and diagnostics as part of the complete data flow.

Keep model, version, SKU and capacity configurable. Do not implement runtime
region fallback or silently substitute another model, SKU or location.
Selection approval does not authorize provisioning, role assignments,
enabling chat or a paid model call.

## Alternatives considered

- GlobalStandard with Australia East resource placement: historically had
  quota and would also allow global processing, but the user explicitly
  selected East US for AI and non-AI resources.
- gpt-5.6-sol Standard in Australia East: investigated for the superseded
  Australia-only requirement; zero quota and unverified regional price.
- gpt-4.1-mini or gpt-4o Standard: also zero quota, with legacy status and earlier
  retirement. Neither solves the current capacity blocker.
- An alternate Australian AI region: not investigated or selected; no longer
  needed to satisfy the superseded Australia-only requirement.
- Provisioned throughput: introduces a commitment that is not justified for
  this educational development workload and has not been approved.

## Consequences

The zero-Standard-quota blocker from Australia East does not apply to the
selected East US GlobalStandard path, which reports available quota.
Allocation and live operation are still unverified. Keep local offline
development available and chat disabled until costs, permissions,
implementation and activation are approved.

Co-locating future regional services in East US avoids the proposed
Australia-to-US application/service split, but developers and users elsewhere
can still incur intercontinental latency. Measure it rather than assuming
global model routing eliminates it.

## Risks

Catalog availability and quota do not prove actual project creation,
retrieval-tool compatibility, deployment capacity or successful inference.
The catalog lists gpt-5-mini inference deprecation on 2027-07-13; plan an upgrade
before production adoption.

The East US public Retail Prices API research identified exact-model priority
(`pp`) and Data Zone meters, but did not establish an unambiguous ordinary
GlobalStandard rate. Do not substitute those prices or another model's rates.
No budget has been established. Budget alerts would not enforce a hard
spending cap.

The source Bicep's gpt-5-mini default version differs from its parameter file.
Only the latter's 2025-08-07 version matched the observed catalog entry.

## Follow-up actions

Obtain verified GlobalStandard pricing, an approved usage/budget envelope and
an explicit deployment capacity. No quota request is currently demonstrated
to be necessary and none was submitted. Validate exact conversation/history
RBAC, service APIs, data handling and source-audit gaps before infrastructure
implementation. Recheck East US catalog/quota immediately before an
explicitly approved Bicep/azd deployment.
Keep the first live test text-only and local-hosted; retain the migration's
Container Apps topology for production rather than introducing a competing
hosted-agent infrastructure definition.

## References

- [Regional model catalog CLI](https://learn.microsoft.com/cli/azure/cognitiveservices/model#az-cognitiveservices-model-list)
- [Deployment types and processing geography](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/deployment-types)
- [Model and Responses data privacy](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy)
- [Agent Service data privacy and security](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/agents/data-privacy-security)
