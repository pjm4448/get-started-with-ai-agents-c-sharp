# Foundry invocation SDK and experimental Responses surface

## Context

The source invokes existing named agents using conversations and the Responses
streaming protocol. It does not merely call a standalone chat-completion model.
SDK verification identified the stable `Azure.AI.Extensions.OpenAI` 3.0.0 release
(2026-11-12), targeting .NET 10 and depending on OpenAI 2.15.0. Despite stable
package versions, the Responses types remain experimental (`AAIP002` and
`OPENAI001`), including the Foundry conversation extension (`SCME0001`).
Package maturity is not a guarantee of API maturity.

## Decision

The user explicitly approved isolated use of this experimental surface.
Pin the Foundry package centrally and lock its dependency graph. Contain SDK
types inside Infrastructure and expose only application-owned stream events.
Suppress only the two Responses diagnostics in the adapter file and the
conversation-extension diagnostic at its single use, with a reference to
this decision. Keep other warnings as errors.

Use the smallest SDK needed to invoke an existing, explicitly versioned agent.
Do not grant runtime agent-authoring privileges or add the broader
`Azure.AI.Projects` package solely to obtain its invocation client.

Require an injected Azure `TokenCredential` and an application-owned HttpClient.
Disable SDK retries for generative POSTs and disable HTTP message/content
logging. The eventual host must select developer credentials locally and an
explicit managed identity in Azure. The host must not add retries around this
adapter or use an HTTP handler that retries POSTs.

The adapter does not create resources, agents, conversations, indexes, or
evaluations. It does not accept a browser-supplied project endpoint or prove
conversation ownership. It remains disconnected from the Web application
until those boundaries and the source audit gates are resolved.

| Capability | SDK / abstraction | Status | Reason | Rejected alternative |
| --- | --- | --- | --- | --- |
| Existing-agent streaming | Azure.AI.Extensions.OpenAI 3.0.0; ProjectResponsesClient behind IAgentClient | Stable package, experimental Responses APIs | Foundry agent references, conversations and typed updates | Standalone model chat would lose agent/retrieval semantics |
| Agent/project administration | Azure.AI.Projects; not yet added | Exact future version and operations require verification | Separate privileged setup boundary | Runtime agent creation would require elevated access |
| Portable chat | Microsoft.Extensions.AI; not added | Not selected for this path | Existing application contract already isolates the provider | Wrapping every Foundry-specific update in another chat abstraction adds no demonstrated value |
| Kernel/plugin orchestration | Semantic Kernel; not added | Not selected | No demonstrated need in this invocation path | Additional orchestration layer would obscure the managed-agent boundary |

## Alternatives considered

- Defer until a non-experimental Responses API: avoids churn but blocks the
  source's core streaming demonstration. The user chose isolated development.
- Invoke a standalone model: not equivalent to the source agent.
- Handwrite the REST/SSE stack: duplicates SDK authentication, deserialization
  and resource-lifetime logic without eliminating the underlying API risk.
- Use the older stable 2.0.0 package: still depends on experimental Responses
  APIs and predates the current strongly typed Azure streaming items.
- Disable experimental diagnostics globally: hides new accidental usage.

## Consequences

Unit tests can exercise real SDK request serialization and SSE deserialization
using an in-memory HTTP handler and a fake credential, without live models.
SDK types do not escape into Application or Blazor.

## Risks

Upstream experimental APIs may change in a stable package. Mapping tests and
locked restore are necessary but do not establish live service compatibility.
Authorization, regional availability, quotas, cancellation cost semantics and
retrieval parity still need separately approved deployment verification.
Stopping stream enumeration does not guarantee that the service stopped all
billable work. Rejecting unsupported tool output locally does not sandbox or
prevent a configured server-side tool from already executing; the eventual
setup boundary must enforce a reviewed tool allowlist.

## Follow-up actions

Review the two upstream changelogs on each intentional upgrade. Once a stable
Responses surface is available, remove the scoped diagnostics and rerun the
request/stream tests. If the surface is removed or materially changed, keep the
application contract and replace only the Infrastructure implementation.
Resolve conversation ownership, configured credentials, resource selection,
timeouts and live verification before connecting the adapter to the UI.

## References

- [Foundry invocation SDK release notes](https://github.com/Azure/azure-sdk-for-net/blob/Azure.AI.Extensions.OpenAI_3.0.0/sdk/ai/Azure.AI.Extensions.OpenAI/CHANGELOG.md)
- [Foundry streaming sample](https://github.com/Azure/azure-sdk-for-net/blob/Azure.AI.Extensions.OpenAI_3.0.0/sdk/ai/Azure.AI.Extensions.OpenAI/samples/Sample12_ResponseStreaming.md)
- [OpenAI 2.15.0 release notes](https://github.com/openai/openai-dotnet/blob/OpenAI_2.15.0/CHANGELOG.md)
