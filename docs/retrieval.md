# Local document-grounded chat

Status: **live grounded chat verified with the initial corpus**. The two
reviewed files were ingested into a vector store and the separate
`aiagents-products` version `1` agent was verified on the configured
workstation. A repeat setup verified existing resources. The workstation now
opts in to this agent; the original text-only agent remains available for
rollback. Building or starting the application never uploads sample documents.
One live answer included an unformatted citation marker in its text; the
separately displayed source label was correct. The transcript now hides the
recognized marker pattern, including the observed missing closing delimiter.
This presentation fix is verified offline, not with a new live model request.

## Initial corpus and prerequisites

The [reviewed samples](../samples/products/README.md) contain two unchanged
source Markdown documents and a SHA-256 manifest. The source MIT notice is
currently absent and must be retained before redistributing the source files.
The other
18 source documents have been reviewed but remain deferred pending individual
decisions and rights/provenance checks. These are sample
claims, not verified product, contact or safety guidance.

A separate [fictional sample corpus](../samples/fictional/README.md) is
available for offline validation. It is not part of the current live agent.
The current version-update command requires retaining the old baseline, so it
cannot use the fictional corpus as a complete replacement without a reviewed
setup change.

Use the configured workstation's .NET 10 SDK and developer Azure credentials
for the same tenant/project as local chat. Web sign-in does not authenticate
the SDK. The console uses Development-only `DefaultAzureCredential` with the
same exclusions as the web host: no environment-secret, workload-identity,
managed-identity, interactive-browser or broker fallback. No API key is used.

The console shares the web project's existing user-secrets ID to read its
named Foundry endpoint and tenant configuration. It does not modify that
store. Standard .NET configuration applies; environment variables override
local user-secrets. `Foundry:ModelDeployment` is required separately because an
agent name is not a model deployment.

## Validate without Azure

From the repository root:

```powershell
dotnet run --project .\tools\AiAgents.Setup -- validate .\samples\products\manifest.json
```

This checks schema, safe leaf filenames, size and exact hashes and takes a
snapshot of the approved bytes. It makes no Azure calls and needs no Azure
credential. Changed documents fail rather than uploading an unreviewed version.
Do not update a hash merely to silence a mismatch.
CI runs the same offline validator. Git attributes preserve sample-file bytes
across checkouts, and manifest JSON whitespace does not invalidate local state.

## Explicit upload and setup boundary

**Obtain approval before running apply.** The operation uploads and retains
the two approved samples in Foundry, creates a managed vector store, waits for
indexing, and creates a separate ordinary prompt agent named
`aiagents-products`. File Search may incur storage/indexing and inference
usage. Confirm the selected setup's retention and data residency; acceptance
of GlobalStandard inference alone is not a data-storage residency guarantee.

Microsoft's [vector store guidance](https://learn.microsoft.com/azure/foundry/agents/concepts/vector-stores)
states that Basic setup uses Microsoft-managed storage and search resources.
Its documented seven-day default applies to stores created by conversation
helpers; do not assume it applies to this separately created agent store.
The [Agent Service privacy guidance](https://learn.microsoft.com/azure/foundry/responsible-ai/agents/data-privacy-security)
and [Models sold by Azure privacy guidance](https://learn.microsoft.com/azure/foundry/responsible-ai/openai/data-privacy)
describe stored data at rest in the resource's geography, while Global model
inference can occur in other geographies. They do not establish a specific
storage region or retention period for this exact Basic setup; verify those
requirements before uploading.

The guide supports Basic and Standard setups, but Standard storage/Search role
requirements must not be copied blindly to this Basic project. The exact
service/model/network combination remains a live verification gate. If it
requires extra infrastructure, permissions or broader networking, stop and
review that change separately. Do not enable keys or trusted-service bypass.

After approval, with the existing correct endpoint/tenant already configured:

```powershell
$env:Foundry__ModelDeployment = "gpt-5-mini"
dotnet run --project .\tools\AiAgents.Setup -- apply .\samples\products\manifest.json .\.local\products-state.json --approve-upload
```

The model deployment is configurable; the command does not create a model.
`Foundry__IngestionTimeout` can override the default `00:05:00` per-file
readiness timeout, up to ten minutes. The command has an overall fifteen-minute
limit and supports Ctrl+C. It never sends a model prompt.

The console uses the existing developer principal. Its inherited privileges
are **not evidence of minimum runtime roles**. It creates no Azure RBAC
assignments, changes no network rules and provisions no ARM resources.
Foundry may generate agent identity metadata as part of agent creation; the
console grants those identities no roles.

### Repeat runs and failures

Keep the ignored `.local` state file. It records the project, model, manifest
hash, remote IDs and pinned agent version, not tokens or document contents.
Repeat the same command with the same state to verify readiness and reuse
resources. It does not select latest or adopt an unrelated existing agent.

- A lock prevents concurrent operations on the same state.
- Every remote write has a durable before/after checkpoint; automatic SDK
  retries are disabled.
- An interrupted write can have an unknown remote outcome. If
  `pendingOperation` is present, **do not delete the state and rerun**. Inspect
  the named operation and remote resources with authorized tools, then
  reconcile the exact ID/checkpoint after verifying its outcome.
- An indexing failure or timeout never creates an agent or becomes success.
  Timeouts preserve uploaded IDs; a later run rechecks readiness without
  reuploading.
- Authentication/authorization failures need the correct tenant/permissions.
  Network denials need the approved connection path, not permissive fallback.
- Changed manifests, unexpected vector-store membership, existing untracked
  agents and mismatched agent readback fail explicitly.
- Nothing is deleted remotely, including on failure. Resource deletion,
  corpus changes or replacing an existing agent require explicit review.
- Do not copy one state file between environments or use multiple independent
  states to create the same agent.

### Preparing an expanded version

The setup tool also provides an **offline-tested, not yet live-verified**
`apply-version` command. It requires a separately reviewed manifest containing
the unchanged baseline files plus at least one additional file, the original
baseline manifest and state, and a distinct new state:

```powershell
dotnet run --project .\tools\AiAgents.Setup -- apply-version <expanded-manifest.json> .\samples\products\manifest.json .\.local\products-state.json <new-state.json> --approve-upload --approve-agent-version
```

Replace the placeholder paths before use. Both flags require prior explicit
approval for the exact files, remote retention and agent-version creation.
The command rechecks the baseline store, pinned definition and latest version,
creates a separate store and checkpoints each write. It does not modify the
baseline state, switch local chat or send a model request. Creating a version
can change the agent's `latest` pointer for other clients. Local locks cannot
prevent changes made from another machine, and the latest check and version
creation are not atomic; coordinate other writers before using this command.
An uncertain write requires manual reconciliation, not an automatic retry.

Server failures are logged by exception type/HTTP status without provider
response bodies. Safe local setup diagnostics identify manifest/readiness
failures. Never troubleshoot by dumping all user-secrets, azd values or tokens.

## Opt in to the retrieval agent

Only after setup has returned and verified its actual pinned version, obtain
approval to switch local chat. Preserve the existing text-only name/version
for rollback; do not assume the new version is `1`.

```powershell
dotnet user-secrets set "Foundry:AgentName" "aiagents-products" --project .\src\AiAgents.Web
dotnet user-secrets set "Foundry:AgentVersion" "RETURNED-VERSION" --project .\src\AiAgents.Web
```

Replace `RETURNED-VERSION` with the verified result. Restart the HTTPS web host,
sign in again, and select **New chat** before sending. Existing bookmarks
must not be treated as retrieval conversations merely because configuration
changed. There is no automatic switch or migration of remote conversations.

Source labels are encoded text. File IDs remain in typed state but are not
displayed or converted into invented download links. Duplicate annotations
are removed during streaming and history restoration. Existing generic tool
progress, cancellation, ownership and no-generative-retry behavior remain.
The transcript removes recognized file-citation markers from assistant text
for display, including across streaming chunks and restored history. The
underlying text is unchanged. Unrecognized marker formats remain visible;
the Sources list, not the marker text, identifies the cited files.

To roll back on this workstation, restore `Foundry:AgentName=aiagents-chat`
and its previously verified `Foundry:AgentVersion=1` using the same two named
user-secret commands, restart, sign in and choose **New chat**. Neither
switching nor New chat deletes remotely stored files/conversations.

## Live verification

A fresh, bounded model-request approval is required for automated checks;
the previous text-only allowance is exhausted. Use the real authenticated
local Blazor UI and the questions in the sample README. Confirm the
document-specific warranty/return facts and matching source labels, follow-up
context, a missing fact without a fabricated answer/source, and source
restoration after refresh. Recheck cancellation/recovery if retrieval changes
that behavior.

On 2026-11-21, five approved synthetic requests in the signed-in local UI
verified the glasses' two-year electronics warranty and 30-day return period,
the blanket's three-year warranty and 45-day guarantee, source labels,
follow-up context, an absent serial number, history after refresh, cancellation
with partial output and recovery in a new chat. Four responses completed; one
was canceled. One comparison answer displayed a raw citation marker alongside
the correct source label. The later presentation fix has offline regression
coverage; these live checks predate it and do not establish that every
provider marker format is rendered cleanly.

Mocked tests establish mapping, ingestion and UI behavior, not actual grounding,
quality, least privilege or multi-user document authorization.

Optional Azure AI Search, the remaining corpus, production deployment and
end-user uploads are outside this initial File Search milestone. See
[ADR 008](decisions/008-file-search-setup.md).
