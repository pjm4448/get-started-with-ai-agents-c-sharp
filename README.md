# Get Started with AI Agents for C#

This migration is in progress. The application cannot be started until Entra
authentication and Foundry agent access are implemented. The current solution
contains typed agent-stream contracts and preliminary Blazor transcript and
composer components. The composer remains disabled and citations are shown
as labels only; they are not yet connected to an agent or conversation history.

The [migration audit](docs/migration/audit.md) records the source inventory,
open investigation items, security decisions and remaining work.

Prerequisite: .NET 10 SDK. Run the initial tests with:

```shell
dotnet test get-started-with-ai-agents-c-sharp.sln --configuration Release
```

The initial pull-request workflow restores locked dependencies, builds, tests,
checks formatting, scans repository files for common Python artifacts and
integrations, and reports vulnerable packages. The scan covers tracked and
unignored files, excluding the historical migration documents and scanner
fixtures. It does not inspect container image layers. Infrastructure,
container, and deployment checks are not yet implemented.