# Reviewed product samples

The initial grounding corpus contains two unchanged Markdown documents from
`Azure-Samples/get-started-with-ai-agents`, audited source commit
`abf8f3dabb2c30c004e9851dde0dc10f98aee6b3`. Their original names are retained.
The source MIT notice is currently absent from this directory. Retain the
source copyright and permission notice before redistributing these documents.
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
Their text and PDF metadata were reviewed; some contain contradictory
safety-sensitive guidance, unsupported specifications or unrelated reviews.
PDF text extraction is not a visual or OCR review. The documents remain
deferred pending individual decisions and rights/provenance checks, not
silently omitted from migration parity. See the
[file-by-file review](../../docs/migration/product-corpus-review.md).
Newly written [fictional samples](../fictional/README.md) are available
separately without copying the additional source documents.

## Example questions

- What is the SmartView Glasses warranty and return period? Cite the document.
- How do those periods compare with the self-warming blanket?
- What is the blanket's serial number? If it is absent, say so without inventing it.

No sample is uploaded by building, validating the manifest, or starting chat.
See [retrieval setup](../../docs/retrieval.md) for the explicit upload boundary.
