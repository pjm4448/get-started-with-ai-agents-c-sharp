# ADR 008: Explicit developer File Search setup

## Context

The source defaults to uploaded-document File Search and optionally supports
Azure AI Search. Text-only local chat already works, but does not demonstrate
grounding. Uploading files, waiting for indexing and authoring agents are
privileged operations, not request-time responsibilities.

## Decision

Add a developer-only .NET setup console, separate from the web runtime. Begin
with two fully reviewed, unchanged, MIT-licensed source documents. Use a
SHA-256-pinned manifest, durable local checkpoints and a separately named
`aiagents-products` agent. Preserve `aiagents-chat` version 1.

Use the installed Azure.AI.Extensions.OpenAI 3.0.0 clients for file/vector-store
operations. Although this package has a stable version, the OpenAI
VectorStoreClient surface is marked **experimental (OPENAI001)**. A narrow
file-level suppression isolates it in the setup implementation. It is necessary
for source-default File Search without introducing another orchestration SDK.
The existing runtime's separately approved experimental surfaces remain
documented in ADR 003.

Use documented Foundry REST v1 only for agent creation/readback; this avoids
adding an administration package solely for two operations. Tokens remain in
memory. Disable SDK retries/content logging and HTTP redirects. Reuse the
Development credential exclusions, tenant and user-secrets configuration.
The console uses Microsoft.Extensions.Hosting 10.0.15 for standard .NET
configuration and structured diagnostic logging.

Keep files, vector stores and agent setup out of web startup and chat requests.
Wait for successful ingestion with a bounded timeout before agent creation.
Read back the pinned agent and ensure it has only File Search against the
expected store. Repeated setup verifies existing readiness and membership.

## Alternatives considered

- Azure AI Search first: remains a later source-parity path with additional
  infrastructure and identity requirements; not synonymous with File Search.
- Hosted agent/toolbox scaffolding or Semantic Kernel: unnecessary for this
  ordinary prompt agent and existing invocation adapter.
- Uploading on application startup: mixes privileges, duplicates work and
  obscures ingestion failures.
- Replacing the working agent or selecting latest: loses a clear rollback
  target and reproducibility.

## Consequences

Uploads require explicit consent and an apply flag. A local state file stores
only endpoint, model, manifest hash and remote IDs, never keys or documents.
An exclusive lock prevents concurrent setup. Every write is checkpointed
before and after the request. If its result is uncertain, future runs stop
for manual reconciliation rather than risk duplicating a remote write.
No automatic deletion, overwrite, role assignment or firewall change occurs.

Runtime citations retain file IDs in application-owned models but display only
encoded source labels. No download link or protected-file proxy is inferred
from an ID. The sample corpus is shared approved data, not a demonstration of
per-user document access control.

## Risks

Experimental APIs can change despite stable package numbering. Direct
SDK/transport tests and package locks constrain upgrades; review the supported
GA replacement and remove the suppression when available.

Local privileged developer success does not establish least privilege.
Current official tool prerequisites describe Foundry User for setup and
storage roles for applicable Standard setups; they do not justify adding those
roles to every runtime. Basic setup, exact runtime permissions, East US/model
compatibility and service-side access through the restricted firewall require
separately approved live verification. Do not provision extra resources or
broaden networking as an implicit fallback.

Uploaded content may remain remotely. GlobalStandard processing is already
accepted, but that is not proof of uploaded-data storage residency. Confirm
the selected service's retention/residency before upload.

## Follow-up actions

- Obtain approval for the initial two-file corpus, resource writes and fresh
  bounded inference before live verification.
- Record actual created IDs/version locally and verify factual answers plus
  citations in the real signed-in UI. Do not equate mocked tests with grounding.
- Complete detailed review of the remaining 18 source documents.
- Implement optional Azure AI Search and isolated runtime-permission testing.
- Revisit experimental APIs at dependency upgrades.

## Live development evidence

The approved two-file setup succeeded in the existing development project and
a repeat run verified the same resources and pinned agent version. Five
approved synthetic requests in the local signed-in UI verified grounding,
source labels, follow-up, refresh, cancellation and recovery. One answer
included a raw citation marker in its text despite displaying the correct
source label. A subsequent offline-tested presentation fix hides the observed
pattern, but other marker formats remain unverified. This check does not
establish least-privilege runtime access or production readiness.

## References

- [Official File Search guide](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/file-search)
- [Foundry REST v1 reference](https://learn.microsoft.com/rest/api/microsoft-foundry/aiproject)
- [Pinned .NET SDK File Search sample](https://github.com/Azure/azure-sdk-for-net/blob/Azure.AI.Extensions.OpenAI_3.0.0/sdk/ai/Azure.AI.Extensions.OpenAI/README.md#file-search)
