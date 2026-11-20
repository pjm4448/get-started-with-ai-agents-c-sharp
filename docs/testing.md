# Testing

## Offline test layers

| Project | Scope |
| --- | --- |
| `AiAgents.UnitTests` | Application authorization, session safety, stream state and actual SDK mapping with an in-memory HTTP transport |
| `AiAgents.ComponentTests` | bUnit interactions, precise input/history limits, protected bookmarks, cancellation and safe rendering |
| `AiAgents.IntegrationTests` | Real cookie-handler sessions, antiforgery, expiration, host configuration, protected endpoints and transport expiry |
| `AiAgents.EndToEndTests` | Real Kestrel HTTPS, Chromium, Interactive Server circuits and browser session storage |

No default test requires Azure resources, Azure sign-in, model calls, the local
Entra application certificate or enabling chat in application configuration.

### File Search setup and citations

The developer-only setup command is tested through the real installed SDK
with an in-memory HTTP transport. Focused cases verify approved-file hashes,
path rejection, successful readiness, repeated setup without writes, failed
indexing, bounded timeout, caller cancellation and checkpoint retention.
An uncertain upload blocks a second attempt rather than duplicating a write;
unexpected remote membership and untracked agents cannot be adopted.
Versioning tests also cover baseline preservation, repeated runs, changed
baseline files, latest-version checks and uncertain version-write recovery.
The expanded-version command has not been exercised against the live service.

Citation tests preserve file IDs across provider mapping and chat/history
state, remove repeated annotations, and render labels as encoded text without
exposing file IDs or constructing download links. bUnit verifies restored
sources without a model request; the existing five offline browser journeys
cover streaming, tool progress, history, cancellation and ownership.

The retrieval implementation passed 52 focused unit tests, 21 component
tests and five offline browser tests. Release solution build completed with
zero warnings/errors; locked restore, formatting, dependency vulnerability
reporting for the setup project and the zero-Python checks passed. The
user-approved live setup ingested both files, verified the pinned
`aiagents-products` agent and successfully repeated setup using the same state.
Five approved synthetic browser requests verified cited sample facts, follow-up,
missing-information handling, refresh, cancellation and recovery. One answer
showed a raw citation marker alongside its source label; rendering that marker
was subsequently fixed and verified with offline transcript and restored-history
tests, without another model request. Unrecognized marker formats can still be
visible. Live success does not establish least privilege.

From the repository root, validate the initial two-file corpus without Azure:

```powershell
dotnet run --project .\tools\AiAgents.Setup -- validate .\samples\products\manifest.json
```

These are offline behavior checks, **not live File Search grounding or
least-privilege evidence**. Uploads and fresh automated model requests require
separate approval. See [retrieval setup and live checks](retrieval.md).

## Fast suite without a browser runtime

From the repository root:

```shell
dotnet test get-started-with-ai-agents-c-sharp.sln --configuration Release --filter "Category!=Browser"
```

The browser project still restores/builds, but its `Category=Browser` tests do
not start. A full solution test run includes the browser tests and requires
their runtime; missing browsers fail explicitly rather than silently skipping.

## Chromium setup and browser suite

Prerequisites: the pinned .NET SDK, PowerShell 7 (`pwsh`) and outbound access
for the initial NuGet/browser downloads. From PowerShell at the repository root:

```powershell
dotnet build tests\AiAgents.EndToEndTests\AiAgents.EndToEndTests.csproj --configuration Release
$env:PLAYWRIGHT_SKIP_BROWSER_GC = '1'
& .\tests\AiAgents.EndToEndTests\bin\Release\net10.0\playwright.ps1 install --only-shell chromium
dotnet test tests\AiAgents.EndToEndTests\AiAgents.EndToEndTests.csproj --configuration Release --no-build --no-restore
```

The installer comes from the centrally pinned `Microsoft.Playwright` package.
Re-run it after intentionally upgrading that package. `--only-shell` downloads
the headless Chromium runtime used by these tests, not other browser engines.
`PLAYWRIGHT_SKIP_BROWSER_GC` prevents removal of other installed browser
revisions. There is no system-browser modification or shared-cache permission
change. On Linux, add `--with-deps` to install the browser's OS dependencies;
the GitHub-hosted CI runner does this.

After building the entire solution, run all test layers with:

```shell
dotnet test get-started-with-ai-agents-c-sharp.sln --configuration Release --no-build --no-restore
```

## Browser journeys

Five tests cover:

- Keyboard-only message submission, incremental text/tool progress, completion,
  citations and usage; refresh restores the owned conversation without another
  create/send. New chat clears the bookmark across refresh.
- Stop preserves partial output, cancels backend enumeration without retry and
  requires New chat before another request.
- Backend failure preserves partial output, displays a safe error and never
  reveals the synthetic private exception message.
- Sign-out through the real antiforgery-protected form in another tab cancels
  active work, disables the old circuit and rejects a copied session cookie.
- A different authenticated owner cannot restore a copied browser bookmark.

Streaming is controlled by a test gate, not sleeps or paid service timing.
Playwright assertions wait for observable UI states. Tests start separate
hosts/browser contexts and dispose their browsers, servers and certificates.
Parallel validation exposed a session-ended/status race; a deterministic
component test defers the session watcher and verifies that operation completion
still gives the sign-in-again message priority over generic cancellation.

## Test-only isolation

All fixture configuration and fakes live in the browser test project.
The application has no test-login endpoint, authentication bypass or mock
provider fallback. The host retains its real cookie scheme, server-side ticket
store, owner resolver and application authorization boundary. The fixture signs
synthetic tenant/user claims through the real cookie handler; it does not
perform an Entra authorization-code exchange.

The fixture binds HTTPS only to IPv4 loopback on a dynamically assigned port.
It generates a short-lived test certificate, imports its key for Windows TLS
compatibility and disposes it at teardown. It never installs a certificate in
a trust store or changes the existing developer/Entra certificate. Only the
isolated Playwright contexts ignore this self-signed certificate's trust error;
normal application TLS validation is unchanged.

Each host has an ephemeral data-protection provider rather than the developer's
persistent key ring. The test project substitutes agent/conversation interfaces,
rejects Azure token acquisition, provides static OIDC discovery metadata and
disables the configured OTLP exporter. Browser page requests to non-test
origins are blocked. API requests use only explicit loopback URLs; cookie-replay
verification disables redirects so it cannot follow the Entra challenge.

## CI and diagnostics

The validation workflow builds, installs the pinned headless Chromium runtime
and runs every test project, including the browser suite. Each project writes
its own `TestResults/tests.trx`; artifact upload preserves their paths so
results are not overwritten by parallel projects.

For detailed local failure output:

```shell
dotnet test tests\AiAgents.EndToEndTests\AiAgents.EndToEndTests.csproj --configuration Release --no-build --logger "console;verbosity=detailed"
```

An executable-not-found error normally means the matching Playwright browser
download has not been installed. TLS errors must be investigated in the test
host; do not fix them by changing machine-wide trust or weakening application
authentication. Failed UI waits include the expected locator/state in the test
output. Only synthetic data should be added to these fixtures.

## Live development verification

On 2026-11-20 the provisioned East US project and gpt-5-mini GlobalStandard
deployment were verified with the text-only `aiagents-chat` prompt agent,
pinned to its returned version `1`. The saved definition was read back:
kind `prompt`, deployment `gpt-5-mini`, and no tools.

The actual credential factory registered by the web application was used to
acquire a token; its tenant and principal matched the approved developer
identity without logging the token. This identity has inherited privileged
roles. It is not evidence that a least-privilege runtime principal can perform
the same operations. A non-inference check through the real application adapter
created and restored an empty synthetic conversation, preserved its owner
marker and read its history.

The user completed real Entra sign-in in the integrated browser at
`https://localhost:7124`. Four synthetic model requests then verified:

| Check | Observed result |
| --- | --- |
| Initial response | Actual model text, completion status and token usage appeared in the signed-in Blazor UI. |
| Follow-up | The model recalled a synthetic marker in the same conversation. |
| Refresh | Both exchanges were restored without resubmitting a prompt. |
| Streaming and cancellation | Partial text was observed before completion; Stop preserved it and displayed the canceled state. Send was disabled and the UI required New chat rather than retrying. |
| Recovery | New chat cleared the previous local transcript; a new request completed successfully. |

The local HTTPS host also returned 200 from `/alive` and `/health`, and an
anonymous root request redirected to Entra. These probes are host-health
checks, not ongoing model-connectivity checks. The host retained normal TLS
certificate validation. No application source changes, added role assignments,
manual identity creation or authentication bypass were needed. Foundry
automatically generated its managed agent identity/blueprint during saved-agent
creation; no roles were granted to it by this setup.

The four-request verification allowance is exhausted. Further automated
inference testing needs new approval; ordinary user-driven chat is separately
initiated by the user. Test conversations remain in Foundry, and New chat
does not delete them. No remote deletion or billing-stop claim is made.

Restart and disable instructions are in the [local setup](../README.md#local-startup).
The environment-specific endpoint and enabled flag are stored only in local
.NET user-secrets; committed configuration keeps chat disabled.

## Remaining verification

The offline journeys do not establish live service behavior; the manual
development results above cover only the operations actually observed.
Isolated least-privilege Azure RBAC permissions/denials, live multi-user
ownership denial, live cross-tab sign-out revocation, remote cancellation or
billing behavior, network-interruption/reconnect semantics,
screen-reader usability, a complete accessibility audit, browser-engine
portability and production cross-replica behavior remain unverified.
Live Azure
verification still requires explicit resource, cost and residency approval.
