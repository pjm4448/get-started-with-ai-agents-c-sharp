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

The existing live agent and its two-file baseline are unchanged. The current
`apply-version` command requires all original baseline documents in the new
manifest, so it cannot install this replacement-only corpus. A separately
reviewed setup change and explicit approval are required before any upload,
remote agent update, local switch or live test. The sample set is Markdown
only; it does not demonstrate PDF ingestion.
