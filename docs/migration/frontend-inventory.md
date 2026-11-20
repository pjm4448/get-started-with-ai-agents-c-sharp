# Frontend and asset inventory

This is a static, preliminary migration mapping. Paths are relative to the
source repository. The chosen target is server-interactive Blazor with standard
CSS unless a component needs a verified additional dependency. No Azure service
is directly accessed by the browser. Most actions remain proposals.
Authenticated local chat has been verified with a Foundry agent.
The transcript supports streamed plain text, tool progress and citation labels;
typed file identities survive stream/history mapping without becoming public
links. Markdown, themes and other source UI parity remain open. Live File Search
was verified with two source documents. A separate fictional corpus is now
indexed; its live chat and citations await manual verification.

Verification shorthand: **UI** means rendering, responsive behavior, keyboard,
focus and accessibility; **chat** means history, streaming, errors and
authenticated isolation; **build** means clean .NET build and static assets.

| Source | Purpose and dependency | Target | Action | Verification |
| --- | --- | --- | --- | --- |
| `src/frontend/package.json` | React/Fluent/Markdown packages and Vite scripts | Central .NET package versions | Replace | Build, license review |
| `src/frontend/pnpm-lock.yaml` | Frontend dependency lock | .NET package lock | Replace | Locked restore |
| `src/frontend/tsconfig.json` | TypeScript frontend compilation | .NET build properties | Replace | Build |
| `src/frontend/tsconfig.node.json` | Vite configuration compilation | .NET build properties | Remove with justification | Build |
| `src/frontend/vite.config.ts` | Produces named frontend bundles | Blazor publishing | Replace | Build, asset caching |
| `src/frontend/src/main.tsx` | React application bootstrap | Blazor host | Replace | Build |
| `src/frontend/src/css-modules.d.ts` | CSS module type declarations | Scoped Blazor CSS | Remove with justification | Build |
| `src/frontend/src/svg.d.ts` | SVG import type declarations | Static assets / Razor | Remove with justification | Build |
| `src/frontend/src/types/chat.ts` | Message and file types | Typed application contracts | Replace | Contract tests |
| `src/frontend/src/components/App.tsx` | Agent metadata load and theme wrapper | Blazor page and services | Replace | UI, chat |
| `src/frontend/src/components/agents/AgentIcon.tsx` | Metadata-selected agent icon | Agent icon component | Port | Missing/unsafe names, UI |
| `src/frontend/src/components/agents/AgentIcon.module.css` | Icon styling | Scoped CSS | Replace | UI |
| `src/frontend/src/components/agents/AgentPreview.tsx` | Screen, history, stream, citations and menus | Page and chat state service | Split | Chat, UI |
| `src/frontend/src/components/agents/AgentPreview.module.css` | Screen layout | Scoped CSS | Replace | UI |
| `src/frontend/src/components/agents/AgentPreviewChatBot.tsx` | Chat list, edit and scroll | Chat component | Partial transcript only | Chat, scrolling, UI |
| `src/frontend/src/components/agents/AgentPreviewChatBot.module.css` | Chat styling | Scoped CSS | Replace | UI |
| `src/frontend/src/components/agents/AssistantMessage.tsx` | Assistant answer and conditional details | Assistant message component | Partial plain text and citation labels | Citations, UI |
| `src/frontend/src/components/agents/AssistantMessage.module.css` | Styles not imported by active component | None unless needed | Remove with justification | UI |
| `src/frontend/src/components/agents/UserMessage.tsx` | User message, timestamp and edit | User message component | Port | Edit-to-composer, UI |
| `src/frontend/src/components/agents/UserMessage.module.css` | Styles not imported by active component | None unless needed | Remove with justification | UI |
| `src/frontend/src/components/agents/BuiltWithBadge.tsx` | Foundry link | Optional badge component | Port | URL safety, UI |
| `src/frontend/src/components/agents/BuiltWithBadge.module.css` | Badge styling | Scoped CSS | Replace | UI |
| `src/frontend/src/components/agents/StarterMessages.tsx` | Starter prompts; no current import established | None unless explicitly enabled | Requires further investigation | Trace usage and UI |
| `src/frontend/src/components/agents/StarterMessages.module.css` | Starter prompt styling | Scoped CSS if needed | Requires further investigation | Trace usage and UI |
| `src/frontend/src/components/agents/UsageInfo.tsx` | Optional usage display | Usage component if data available | Requires further investigation | Actual usage data, UI |
| `src/frontend/src/components/agents/UsageInfo.module.css` | Usage display styling | Scoped CSS if needed | Requires further investigation | UI |
| `src/frontend/src/components/agents/Waves.tsx` | Canvas animation and reduced-motion behavior | CSS or browser interop | Replace | Reduced motion, UI |
| `src/frontend/src/components/agents/chatbot/ChatInput.tsx` | Composer, validation and generating state | Blazor input component | Preliminary disabled composer; interactions not yet verified | Keyboard, duplicates, cancellation |
| `src/frontend/src/components/agents/chatbot/ChatInput.module.css` | Styles not imported by active composer | None unless needed | Remove with justification | UI |
| `src/frontend/src/components/agents/chatbot/types.ts` | Chat, annotation and callback types | Application event/state contracts | Replace | Contract tests |
| `src/frontend/src/components/agents/hooks/useFormatTimestamp.ts` | Local message timestamp format | .NET formatting | Replace | Locale and time-zone tests |
| `src/frontend/src/components/agents/hooks/useIsMotionReduced.ts` | Reduced-motion media query | CSS / browser interop | Replace | Preference changes |
| `src/frontend/src/components/core/Markdown.tsx` | Markdown, math, syntax highlighting, sanitization and think blocks | Vetted .NET Markdown renderer | Replace | XSS, links, code, math, partial markup |
| `src/frontend/src/components/core/Markdown.module.css` | Markdown styling | Scoped CSS | Replace | UI |
| `src/frontend/src/components/core/MenuButton/MenuButton.tsx` | Menu abstraction | Accessible Blazor menu | Replace | Keyboard and focus |
| `src/frontend/src/components/core/MenuButton/MenuButton.module.css` | Menu styling | Scoped CSS | Replace | UI |
| `src/frontend/src/components/core/SettingsPanel.tsx` | Settings drawer | Blazor component | Port | Dismiss/focus, UI |
| `src/frontend/src/components/core/SettingsPanel.module.css` | Settings styling | Scoped CSS | Replace | UI |
| `src/frontend/src/components/core/ThinkBlock.tsx` | Expandable timed thinking content | Blazor component | Port | Partial and completed content, accessibility |
| `src/frontend/src/components/core/ThinkBlock.module.css` | Thinking-block styling | Scoped CSS | Replace | UI |
| `src/frontend/src/components/core/theme/ThemeContext.ts` | Theme state | Blazor state service | Replace | UI |
| `src/frontend/src/components/core/theme/ThemePicker.tsx` | Theme selection | Blazor component | Port | Keyboard and persistence |
| `src/frontend/src/components/core/theme/ThemeProvider.tsx` | Theme context/provider | Blazor state service | Replace | UI |
| `src/frontend/src/components/core/theme/themes.ts` | Theme palettes | CSS tokens | Replace | Contrast |
| `src/frontend/src/components/core/theme/useThemeProvider.ts` | System theme and browser persistence | CSS / browser interop | Replace | Storage and preference changes |
| `src/frontend/src/components/icons/AIFoundryLogo.tsx` | Foundry mark | Asset or component after rights review | Requires further investigation | Rights, UI |

## Knowledge documents

The following source files are separately enumerated knowledge inputs. The
proposed target is a sample-data directory used by the setup/ingestion
operation, subject to provenance and redistribution review. Both managed File
Search and optional Azure AI Search depend on them. Verify each file's upload,
index completion and citation label. The initial target corpus includes only
`product_info_1.md` and `product_info_2.md`, unchanged with the source MIT
notice and SHA-256 manifest. The other 18 remain deferred after
[text and metadata review](product-corpus-review.md); rights and complete
PDF inspection remain unresolved.

| Source | Description | Action |
| --- | --- | --- |
| `src/files/product_info_1.md` | Product document | Retain unchanged |
| `src/files/product_info_2.md` | Product document | Retain unchanged |
| `src/files/product_info_3.md` | Product document | Requires further investigation |
| `src/files/product_info_4.md` | Product document | Requires further investigation |
| `src/files/product_info_5.md` | Product document | Requires further investigation |
| `src/files/product_info_6.md` | Product document | Requires further investigation |
| `src/files/product_info_7.md` | Product document | Requires further investigation |
| `src/files/product_info_8.md` | Product document | Requires further investigation |
| `src/files/product_info_9.md` | Product document | Requires further investigation |
| `src/files/product_info_10.pdf` | Product PDF | Requires further investigation |
| `src/files/product_info_11.pdf` | Product PDF | Requires further investigation |
| `src/files/product_info_12.pdf` | Product PDF | Requires further investigation |
| `src/files/product_info_13.pdf` | Product PDF | Requires further investigation |
| `src/files/product_info_14.pdf` | Product PDF | Requires further investigation |
| `src/files/product_info_15.pdf` | Product PDF | Requires further investigation |
| `src/files/product_info_16.pdf` | Product PDF | Requires further investigation |
| `src/files/product_info_17.pdf` | Product PDF | Requires further investigation |
| `src/files/product_info_18.pdf` | Product PDF | Requires further investigation |
| `src/files/product_info_19.pdf` | Product PDF | Requires further investigation |
| `src/files/product_info_20.pdf` | Product PDF | Requires further investigation |

## Static images

These are under `src/api/static/assets/template-images/`. The default avatar
and metadata-selected icon path are active; usage of other images is not yet
established. Common dependencies are the React icon component and static
server, not an Azure service. Proposed target is Blazor `wwwroot` where
actually used; verify URLs, filenames containing spaces, appearance, fallback,
alt text and rights. Brand assets need trademark/provenance review.

| Source file | Purpose inferred from name | Action |
| --- | --- | --- |
| `AgentExploreBackground.png` | Agent background | Requires further investigation |
| `AgentExploreCard_Dark.svg` | Dark agent card | Requires further investigation |
| `AgentExploreCard_Light.svg` | Light agent card | Requires further investigation |
| `Avatar_Code.svg` | Code avatar | Requires further investigation |
| `Avatar_Consumer.svg` | Consumer avatar | Requires further investigation |
| `Avatar_Default.svg` | Default avatar | Retain unchanged, subject to rights |
| `Avatar_Financial Services.svg` | Financial-services avatar | Requires further investigation |
| `Avatar_Healthcare.svg` | Healthcare avatar | Requires further investigation |
| `Avatar_Industry.svg` | Industry avatar | Requires further investigation |
| `Avatar_Travel.svg` | Travel avatar | Requires further investigation |
| `AzureSearch.svg` | Search icon | Requires further investigation |
| `Bing.svg` | Bing icon | Requires further investigation |
| `Code.svg` | Code artwork | Requires further investigation |
| `CodeInterpreter.svg` | Code-interpreter icon | Requires further investigation |
| `Consumer.svg` | Consumer artwork | Requires further investigation |
| `Financial Services.svg` | Financial-services artwork | Requires further investigation |
| `Healthcare.svg` | Healthcare artwork | Requires further investigation |
| `Industry.svg` | Industry artwork | Requires further investigation |
| `Microsoft.svg` | Microsoft mark | Requires further investigation |
| `OpenApi.svg` | OpenAPI icon | Requires further investigation |
| `ToolsExploreBackground.png` | Tools background | Requires further investigation |
| `ToolsExploreCard_Dark.svg` | Dark tools card | Requires further investigation |
| `ToolsExploreCard_Light.svg` | Light tools card | Requires further investigation |
| `Travel.svg` | Travel artwork | Requires further investigation |

## Documentation images

These are under `docs/images/`. They depend on the associated source
documentation, not an Azure SDK at runtime. Replace screenshots that show
the old UI, authentication or deployment; retain only if accurate and rights
allow. Verify links, redaction, alt text and current portal appearance. Names
below describe filenames/document references, not verified image contents.

| Source file | Purpose | Action |
| --- | --- | --- |
| `architecture.png` | Architecture diagram | Replace |
| `webapp_screenshot.png` | Chat screenshot | Replace |
| `agent_id_in_foundry_ui.png` | Agent ID | Requires further investigation |
| `agent_monitor.png` | Monitoring | Requires further investigation |
| `ai_search_datasource.png` | Search data source | Requires further investigation |
| `ai_search_indexers.png` | Search indexers | Requires further investigation |
| `ai_search_indexes.png` | Search indexes | Requires further investigation |
| `ai_search_skillsets.png` | Search skillsets | Requires further investigation |
| `app_insight_logs_query.png` | Logs query, no reference found | Requires further investigation |
| `blob_store_containers.png` | Blob containers | Requires further investigation |
| `enable_cont_eval.png` | Continuous evaluation | Requires further investigation |
| `tracing_eval_screenshot.png` | Tracing, no reference found | Requires further investigation |
| `tracing_tab.png` | Tracing tab | Requires further investigation |
| `vs_code_launch.png` | Editor launch | Replace |
| `azure-app-service-auth-setup/AddDetails.png` | Registration details | Replace |
| `azure-app-service-auth-setup/AddPlatform.png` | Platform setup | Replace |
| `azure-app-service-auth-setup/AddRedirectURL.png` | Redirect setup | Replace |
| `azure-app-service-auth-setup/AppAuthIdentityProvider.png` | Identity provider | Replace |
| `azure-app-service-auth-setup/AppAuthIdentityProviderAdd.png` | Provider addition | Replace |
| `azure-app-service-auth-setup/AppAuthIdentityProviderAdded.png` | Added provider | Replace |
| `azure-app-service-auth-setup/AppAuthentication.png` | App Service authentication | Replace |
| `azure-app-service-auth-setup/AppAuthenticationIdentity.png` | Authentication identity | Replace |
| `azure-app-service-auth-setup/Appregistrations.png` | Registrations | Requires further investigation |
| `azure-app-service-auth-setup/MicrosoftEntraID.png` | Entra portal | Requires further investigation |
| `azure-app-service-auth-setup/NewRegistration.png` | Registration | Requires further investigation |
| `azure-app-service-auth-setup/Web.png` | Web platform | Requires further investigation |
| `azure-app-service-auth-setup/WebAppURL.png` | App URL | Replace |
