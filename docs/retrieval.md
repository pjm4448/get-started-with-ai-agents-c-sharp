# Local document-grounded chat

Status: **fictional replacement installed; manual chat reported successful**.
The initial two-file corpus was verified with `aiagents-products` version `1`.
The 20 fictional records have since been indexed in a separate store and
version `2` was read back and selected on this workstation. Repeat setup
verified existing resources. Version `1` and the text-only agent remain
available for rollback. Building or starting the application never uploads
sample documents.
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
indexed by the current local agent version. The user reports that manual
chat went fine; individual prompts and citation checks were not reported.
An additional [fictional PDF](../samples/fictional-search/README.md) is
prepared for the planned Azure AI Search mode, but has not been uploaded or
indexed.

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

For the planned Search mode, the setup tool can also preview an index definition
without contacting Azure. This example uses a fictitious endpoint:

```powershell
dotnet run --project .\tools\AiAgents.Setup -- preview-search-index fictional-index https://example.openai.azure.com embedding text-embedding-3-small 1536
dotnet run --project .\tools\AiAgents.Setup -- preview-search-skillset fictional-index https://example.openai.azure.com embedding text-embedding-3-small 1536
dotnet run --project .\tools\AiAgents.Setup -- preview-search-indexer fictional-index markdown
dotnet run --project .\tools\AiAgents.Setup -- preview-search-indexer fictional-index pdf
dotnet run --project .\tools\AiAgents.Setup -- preview-search-datasource fictional-index markdown /subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/example/providers/Microsoft.Storage/storageAccounts/exampleaccount fictional-corpus
dotnet run --project .\tools\AiAgents.Setup -- preview-search-datasource fictional-index pdf /subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/example/providers/Microsoft.Storage/storageAccounts/exampleaccount fictional-corpus
```

The generated definition includes a keyword-searchable chunk, a hidden vector,
a vectorizer and a filterable parent identifier. It uses the Search service's
system-assigned identity for the vectorizer, not an API key. Model and dimension
choices must also match the eventual embedding skill. This preview accepts
only the models' default dimensions because the query vectorizer definition
has no dimensions setting. The skillset preview splits extracted content,
generates matching embeddings and projects chunks with their source metadata.
These previews print their
resource endpoint; avoid sharing output containing a real environment's
configuration. Data-source previews print the Storage resource ID, not an
account key, and select separate `markdown/` and `pdf/` virtual folders.
They do not create resources, validate model availability or prove network
access. The indexer previews are disabled, use private execution and allow no
failed items. Their parser
and projection compatibility, Markdown parsing and PDF extraction remain
unverified; live setup is not implemented yet.

The setup project also contains a guarded Blob-upload foundation, exercised
offline but not exposed as a command. It snapshots both validated manifests,
requires a private dedicated container, rejects untracked or missing Blobs,
uses conditional creation, and verifies bytes against the returned ETag.
Separate local state records pending writes before sending them. An uncertain
write or interrupted readback blocks subsequent attempts until manually
reconciled; no automatic deletion or retry is performed. The complete Search
setup, indexing, agent creation and live upload are not yet available.
An offline-tested readback helper checks both indexer results against captured
pre-run execution timestamps, rejects warnings and failures, and checks indexed
chunk IDs, source membership and selected expected text for every file. It deliberately
rejects 1,000 or more chunks and count mismatches rather than accepting a
possibly truncated result set. It must be integrated with a checkpointed run
workflow; these checks have not yet been exercised against a live index.

For URI citations, the transcript does not render the URI. It displays the
provider label only when it is a simple Markdown or PDF filename matching
the URI's final path segment; otherwise it displays `Source`. This reduces
accidental exposure of URL and query-string labels, but does not establish
that a provider-supplied filename is non-sensitive. File Search citations
continue to display their encoded file labels.

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

### Replacing the corpus with fictional samples

The separate `replace-version` path accepts a replacement manifest instead of
requiring the original documents in the new store. It checks the recorded
baseline locally and remotely but rejects replacement files with baseline
names or identical content. It does not reupload the baseline. The new
version's instructions identify the samples as fictional and not product or
safety guidance; instructions alone are not a safety guarantee.

After confirming the exact fictional files, retention implications and impact
on other clients, the approved command is:

```powershell
dotnet run --project .\tools\AiAgents.Setup -- replace-version .\samples\fictional\manifest.json .\samples\products\manifest.json .\.local\products-state.json .\.local\fictional-state.json --approve-replacement --approve-upload --approve-agent-version
```

The command requires all three flags. It uses a new vector store and distinct
checkpoint state, retains the baseline version and store, and verifies the
returned version. `apply` and `apply-version` keep their existing behavior.
Local state locks cannot prevent other machines from changing `latest` between
the last check and the write. A pending write must be reconciled, not retried.
Switch local chat only after the new pinned version has been verified, and
preserve the previous version for rollback. Neither setup nor switching sends
a model request. The fictional corpus contains Markdown only; PDF ingestion
remains unverified by this corpus.

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
The prior retrieval corpus is also still available as
`Foundry:AgentName=aiagents-products`, `Foundry:AgentVersion=1`.

## Live verification

A fresh, bounded model-request approval is required for automated checks.
The user chose to test version `2` manually. In the authenticated local UI,
start **New chat**, use the questions in the
[fictional sample README](../samples/fictional/README.md), and check source
labels, an absent fact, refresh and cancellation. The older warranty/return
checks below apply only to version `1`, not to the fictional replacement.

On 2026-11-21, replacement setup ingested all 20 fictional files, verified
exact store membership and the pinned version `2` definition, and completed
a repeat run without changing either local state file. Readback confirmed
`latest` is `2` and version `1` still exists. Local chat was pinned to `2`;
health and liveness returned 200. The user subsequently reported that manual
chat went fine; the individual prompts and citation checks were not reported.

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
