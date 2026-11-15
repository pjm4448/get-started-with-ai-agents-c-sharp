# Operations and documentation inventory

Paths are relative to the source repository. These are proposed actions and
verification, not completed migration work. Unless otherwise noted, the
target runtime is .NET 10; documentation and repository policy files have no
Azure runtime dependency.

An automated permission check denied a search of the eight scripts because
its output could reveal reusable secrets. This was not identified as an
organization content-exclusion policy. Script internals remain unverified;
their names and references in accessible configuration are not proof of
complete behavior. Do not copy credential-handling code or claim those scripts
have been fully audited.

| Source | Purpose and dependencies | Proposed target / framework | Azure dependency | Action | Verification |
| --- | --- | --- | --- | --- | --- |
| `README.md` | Overview and user guidance | Root README | Foundry and deployment | Replace | Test links and commands |
| `next-steps.md` | Deployment next steps; contains stale references | Deployment documentation | azd, ACA | Replace | Validate commands and paths |
| `azure.yaml` | azd services, hooks and environment inputs | Target `azure.yaml`; azd | ACA, Foundry and resources | Replace | Validate inputs, provisioning and deployment |
| `docker-compose.yaml` | Local container and environment | Aspire AppHost | Optional Azure services | Remove with justification | Local Aspire startup |
| `requirements-dev.txt` | Original test and lint dependencies | .NET test/build dependencies | None | Replace | Restore and tests |
| `.gitignore` | Environment and build exclusions | Target `.gitignore` | None | Replace | Check local secrets are ignored |
| `LICENSE` | MIT terms and attribution | Applicable target license and attribution | None | Retain unchanged | Review license and notices |
| `CODE_OF_CONDUCT.md` | Community conduct policy | Community policy | None | Consolidate | Check owner policy and links |
| `CONTRIBUTING.md` | Contribution/CLA guidance | Contribution guide | None | Replace | Validate repository links |
| `SECURITY.md` | Security reporting | Security policy | None | Replace | Verify reporting route |
| `SUPPORT.md` | Support guidance | Support guidance | None | Replace | Verify ownership and links |
| `.github/CODE_OF_CONDUCT.md` | GitHub conduct policy | Community policy | None | Consolidate | Check duplicate and links |
| `.github/CONTRIBUTING.md` | Contribution guidance | Contribution guide | None | Consolidate | Check duplicate and links |
| `.github/ISSUE_TEMPLATE.md` | Issue template | GitHub issue template | None | Replace | Preview issue flow |
| `.github/SECURITY.md` | Security reporting | Security policy | None | Consolidate | Verify private reporting |
| `.github/skills/up/SKILL.md` | Guided deployment and prechecks | Optional target deployment guidance | azd and Azure | Requires further investigation | Validate against target tooling |
| `.github/skills/up/up-example.md` | Guided deployment example | Optional target example | azd and Azure | Requires further investigation | Compare to actual flow |
| `.github/workflows/azure-dev.yml` | OIDC provision/deploy workflow | Deployment workflow; GitHub Actions | Azure and ACR | Replace | OIDC, immutable image and smoke test |
| `.github/workflows/template-validation.yml` | Manual template validation | Validation workflow; GitHub Actions | Azure when live | Replace | Least permissions and input coverage |
| `.devcontainer/devcontainer.json` | Source development container and tools | .NET development container if justified | Azure CLI/azd optional | Replace | Start and build without Python |
| `.vscode/launch.json` | Source debugging profiles | .NET/Aspire launch profiles | Foundry optional | Replace | Launch/debug |
| `.vscode/settings.json` | Source test discovery settings | .NET editor settings if needed | None | Replace | Test discovery |
| `scripts/postdeploy.ps1` | Post-deploy hook; internals incompletely inspected | Target post-deploy guidance | azd/ACA | Requires further investigation | Review approved behavior, output and exit status |
| `scripts/postdeploy.sh` | POSIX post-deploy hook; internals restricted | Target post-deploy guidance | azd/ACA | Requires further investigation | Review approved behavior and platform parity |
| `scripts/resolve_model_quota.ps1` | Quota resolver; internals incompletely inspected | Supported preflight flow | Foundry quota | Requires further investigation | Review quota handling and errors |
| `scripts/resolve_model_quota.sh` | POSIX quota resolver; internals restricted | Supported preflight flow | Foundry quota | Requires further investigation | Review parity and errors |
| `scripts/setup_credential.ps1` | Basic-auth setup; internals restricted | Entra-based setup documentation | ACA / Entra | Requires further investigation | Review existing behavior and replace secrets safely |
| `scripts/setup_credential.sh` | POSIX basic-auth setup; internals restricted | Entra-based setup documentation | ACA / Entra | Requires further investigation | Review platform parity and replacement |
| `scripts/validate_env_vars.ps1` | Pre-up input validation; internals incompletely inspected | Target configuration preflight | azd | Requires further investigation | Review validation coverage and exit status |
| `scripts/validate_env_vars.sh` | POSIX pre-up validation; internals restricted | Target configuration preflight | azd | Requires further investigation | Review validation coverage and exit status |
| `docs/ai_search.md` | Search setup, indexing and update workflow | Retrieval documentation | Search, Blob and Foundry | Replace | Verify both ingestion modes |
| `docs/azure_account_setup.md` | Account prerequisites | Prerequisite documentation | Azure account | Replace | Verify prerequisites |
| `docs/azure_app_service_auth_setup.md` | App Service authentication | Entra/ACA auth documentation | Entra, ACA | Replace | Verify target auth |
| `docs/create_new_app_registration.md` | Entra app registration | Entra setup guide | Entra | Replace | Review tenant, redirect and authorization requirements |
| `docs/deploy_customization.md` | Existing resources, models and feature configuration | Deployment customization | Foundry, Azure | Replace | Validate all target parameters |
| `docs/deployment.md` | Deployment paths and configuration | Target deployment guide | azd and Azure | Replace | Test commands from clean environment |
| `docs/local_development.md` | Local setup and debugging | Aspire development guide | Optional Azure | Replace | Verify local startup and debugging |
| `docs/observability.md` | Tracing, evaluations and red teaming | Observability/evaluation guide | Monitor and Foundry | Replace | Verify privacy, reports and opt-in |
| `docs/sample_questions.md` | Example knowledge questions | Sample questions | Foundry retrieval | Port | Verify answers and citations |
| `docs/troubleshooting.md` | Troubleshooting | Target troubleshooting | Azure and local tools | Replace | Check links and diagnostics |
| `src/Dockerfile` | Builds source runtime and frontend image | One .NET container build approach | ACA/ACR | Replace | Non-root, image scan, health |
| `src/.dockerignore` | Container build exclusions | Target build context exclusions | None | Replace | Inspect context for secrets |
| `src/requirements.txt` | Runtime dependencies; has existing user changes | Central .NET package versions | Azure SDKs | Replace | SDK compatibility and locked restore |
| `src/pyproject.toml` | Package metadata; has existing user changes | .NET projects and build properties | Azure SDKs | Replace | Build and dependency review |
| `tests/test_evaluation.py` | Live evaluator creation and polling | Opt-in live evaluation tests | Foundry | Port | Bounded polling, result thresholds and cleanup |
| `tests/test_red_teaming.py` | Live prohibited-action taxonomy and red-team evaluation | Opt-in live safety tests | Foundry | Port | Bounded polling, outcomes and cleanup |
| `tests/test_utils.py` | Environment and agent lookup helpers | Typed test configuration and helpers | Foundry | Replace | Missing/malformed input tests |

The original live tests create remote evaluations and assert completion rather
than a passing quality or safety threshold. They have no evident bounded
polling or cleanup. Default target tests must use fakes and never incur Azure
costs.
