# Fictional PDF retrieval sample

This independently authored PDF describes an imaginary exhibition prop.
It is not a real product, specification or safety instruction. It is
provided for a future Azure AI Search PDF-indexing demonstration.

The PDF has not been uploaded or indexed.

The Paper Festival lantern's exhibit tag is `PF-21` and its display colour
is teal. The [manifest](manifest.json) pins the PDF's exact bytes.
Validate it together with the fictional Markdown corpus without Azure:

```powershell
dotnet run --project .\tools\AiAgents.Setup -- validate-search .\samples\fictional\manifest.json .\samples\fictional-search\manifest.json
```
