# Fictional retrieval samples

These 20 newly written Markdown records describe imaginary exhibition props.
They are not real products, instructions, technical specifications, safety
advice, warranties or contact details. Their names, exhibit tags, colours and
short stories provide distinct facts for retrieval and citation exercises.
They do not reproduce the source product documents.

Validate the exact bytes without Azure or uploads:

```powershell
dotnet run --project .\tools\AiAgents.Setup -- validate .\samples\fictional\manifest.json
```

For example, ask which exhibit tag and display colour belong to the Cloud
Gallery jacket, or compare the collections containing pretend stoves. A
question about an absent serial number should be answered as unknown.
These are proposed questions only: automated model requests need separate
approval.

The replacement setup has indexed these files in a separate store and the
local app is pinned to `aiagents-products` version `2`. The old version and
store remain available. The user reports that manual version `2` chat went
fine; specific prompts and citation checks were not reported.
`replace-version` checks the original baseline without reuploading it. See
[retrieval setup](../../docs/retrieval.md) for retention, versioning and
rollback details. The sample set is Markdown only; it does not demonstrate PDF
ingestion.
