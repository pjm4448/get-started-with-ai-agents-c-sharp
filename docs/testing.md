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

## Remaining verification

These journeys do not establish real Entra sign-in/certificate redemption,
live Foundry SDK compatibility, Azure RBAC permissions/denials, remote
cancellation or billing behavior, network-interruption/reconnect semantics,
screen-reader usability, a complete accessibility audit, browser-engine
portability or production cross-replica behavior. Recheck real browser sign-in
and sign-out on the configured development host separately. Live Azure
verification still requires explicit resource, cost and residency approval.
