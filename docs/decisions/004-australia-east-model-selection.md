# Australia East deployment and model selection

## Context

The user selected preparation of a new Foundry project in Australia East under
the previously confirmed tenant and subscription. This is not authorization
to provision resources. The migration requires separate primary and AI
locations, defaulting both to `australiaeast`, and forbids silent fallback.

The user subsequently confirmed that processing must remain within Australia
and selected the existing local application plus a text-only Foundry agent as
the first live milestone. Retrieval and cloud hosting remain later phases.
The budget response was blank; there is no approved monthly spending amount.

Initial read-only regional catalog and subscription usage queries on
2026-11-17 found:

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

These are catalog/permissioned usage observations, not live inference results.
The exact quota record for gpt-4.1-mini is
`OpenAI.Standard.gpt4.1-mini`; do not infer usage identifiers from model names.
See the [deployment proposal](../../.azure/deployment-plan.md) for all record
names, API evidence, pricing limitations and approval gates.

## Decision

Keep both proposed locations in Australia East. Do not create an alternate
region, runtime fallback or deployment until explicitly approved.

Exclude the source's gpt-5-mini GlobalStandard combination from selection
under the confirmed Australia-only requirement. APAC Data Zone is also broader
than Australia and is not an acceptable substitute.

Prefer investigating the GA gpt-5.6-sol Standard candidate and an Australia
East quota increase over selecting a legacy model. **No regional candidate is
currently deployable on the reported quota.** No model, version, capacity,
quota request or spending has been approved.

Standard constrains inference to an Azure geography, not a single Azure
region. Review the Australia geography and the whole saved-agent path,
including conversation storage, orchestration, content safety, abuse
monitoring and diagnostics. Do not equate an Australian ARM resource location
with complete processing-residency verification.

Do not silently substitute a model to avoid a quota or residency constraint.
Keep deployment model, version, SKU and capacity configurable.

## Alternatives considered

- GlobalStandard with Australia East resource placement: aligns with the source
  model and has reported quota, but is excluded by the confirmed requirement.
- gpt-5.6-sol Standard: GA catalog candidate for Australian processing;
  zero quota, unverified regional price and live compatibility prevent selection.
- gpt-4.1-mini or gpt-4o Standard: also zero quota, with legacy status and earlier
  retirement. Neither solves the current capacity blocker.
- An alternate Australian AI region: not investigated or selected by this
  decision; requires explicit user selection and documented residency, latency,
  networking, compliance and cost consequences. A non-Australian fallback is
  outside the confirmed requirement.
- Provisioned throughput: introduces a commitment that is not justified for
  this educational development workload and has not been approved.

## Consequences

Keep local offline development available, with chat disabled by default.
Regional deployment is blocked until capacity is obtained and costs,
residency, permissions and implementation are approved. Do not bypass these
gates by substituting a global SKU or provisioned capacity.

## Risks

Catalog availability and quota do not prove actual project creation,
retrieval-tool compatibility, deployment capacity or successful inference.
The catalog lists gpt-5-mini inference deprecation on 2027-07-13; it remains
historical source-parity evidence, not the selected regional model.

The public Retail Prices API query for Australia East gpt-5.6-sol meters
returned only global SKU prices. Billing-region metadata does not turn these
into regional Standard prices. No reliable regional estimate or budget has
been established. Budget alerts would not enforce a hard spending cap.

The source Bicep's gpt-5-mini default version differs from its parameter file.
Only the latter's 2025-08-07 version matched the observed catalog entry.

## Follow-up actions

Obtain regional pricing, an approved usage/budget envelope and a decision on
requesting Australia East quota. No quota request was submitted. Validate
whole-path Australian processing, exact conversation/history RBAC, service
APIs and source-audit gaps before infrastructure implementation. Recheck
catalog/quota immediately before an explicitly approved Bicep/azd deployment.
Keep the first live test text-only and local-hosted; retain the migration's
Container Apps topology for production rather than introducing a competing
hosted-agent infrastructure definition.

## References

- [Regional model catalog CLI](https://learn.microsoft.com/cli/azure/cognitiveservices/model#az-cognitiveservices-model-list)
- [Deployment types and processing geography](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/deployment-types)
- [Model and Responses data privacy](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy)
- [Agent Service data privacy and security](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/agents/data-privacy-security)
