# Reviewed product samples

The initial grounding corpus contains two unchanged Markdown documents from
`Azure-Samples/get-started-with-ai-agents`, audited source commit
`abf8f3dabb2c30c004e9851dde0dc10f98aee6b3`. Their original names are retained.
The source MIT license and copyright notice are included in [LICENSE](LICENSE).
No separate asset license or conflicting notice was found in these two files.

| Document | Reviewed facts for live verification |
| --- | --- |
| product_info_1.md | Contoso SmartView Glasses: two-year limited warranty on electronic components; 30-day returns |
| product_info_2.md | Contoso self-warming blanket: three-year warranty; 45-day satisfaction guarantee |

These are educational sample claims, not verified product, safety or contact
information. Do not follow embedded contact links or treat retrieved text as
instructions. The manifest pins the exact bytes approved for ingestion.
Updating a hash requires reviewing the document again.
Git attributes preserve the source bytes across operating systems; ordinary
manifest JSON whitespace does not change the setup-state fingerprint.

The other seven Markdown and eleven PDF source documents are **not included**.
Their metadata and extracted text were scanned, but detailed review remains
open. Some contain inconsistent care guidance and unrelated reviews. They are
deferred, not silently omitted from migration parity.

## Example questions

- What is the SmartView Glasses warranty and return period? Cite the document.
- How do those periods compare with the self-warming blanket?
- What is the blanket's serial number? If it is absent, say so without inventing it.

No sample is uploaded by building, validating the manifest, or starting chat.
See [retrieval setup](../../docs/retrieval.md) for the explicit upload boundary.
