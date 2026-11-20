# Additional product-document review

The original source repository contains seven further Markdown documents and
eleven PDFs beyond the two in the current sample corpus. The user selected all
18 for further consideration and prefers the original files. No independent
rights evidence was provided, and none is approved for redistribution or
upload. They remain **deferred** pending resolution. The source repository
has an MIT license, but the provenance and redistribution rights of individual
assets have not been independently established. The product, warranty, contact
and safety claims are not verified and must not be used as consumer guidance.

Source Git history records the additional Markdown files in commit `d28bde8`.
Commit `c1d210f` later added the numbered PDFs while removing same-numbered
Markdown files. The [source pull request](https://github.com/Azure-Samples/get-started-with-ai-agents/pull/208)
describes converting some Markdown files to PDF, but does not establish
authorship or independent rights clearance for the underlying material.

The Markdown text was reviewed directly. PDF review used extracted text from
every page and inspected metadata, page images and attachments exposed by the
parser. It reported minor cross-reference warnings, and no page images or
attachments through those interfaces. This was not visual inspection or OCR;
unsupported or hidden PDF content cannot be ruled out. PDF completeness
therefore remains unresolved. Hashes below identify the bytes reviewed, not
proof of authorship, accuracy, permission or future approval.

| Source file | SHA-256 | Review finding |
| --- | --- | --- |
| `product_info_3.md` | `00B08D07FF6332746C3D4B7893F4FFC7FF49597C3FFCFA0D0BFF6CC2B0EC6DB1` | Jacket; unverified care and product claims. |
| `product_info_4.md` | `9AB4D85834F88A8A68159FC1D58B123573CB7556FC893D8A7D58237472AF8251` | Boots; conflicting descriptions of water protection. |
| `product_info_5.md` | `BE0359065054EA79D91F22FA03DE7AC56AB5938B5C0FAFAA5BF401E1F6BA9C53` | Table; capacity stated in one section but absent from specifications. |
| `product_info_6.md` | `D18C5F7424DE68CA0E4B8C5E41DD50F9AB851C50D6CD2074AC07C7210571E5FA` | Stove; conflicting, safety-sensitive fuel and ignition guidance. |
| `product_info_7.md` | `18BD11357936E3052EB4E01F8D8B75AEDF02D57653F6BBFE92F4151902309F66` | Sleeping bag; conflicting ratings and an unrelated warranty section. |
| `product_info_8.md` | `E311CA17EEA57A6621EE36AFBEFE2558BB592821DC6911DC4798A22722DE40E7` | Tent; generic claims and an inappropriate caution. |
| `product_info_9.md` | `21232FF88ECF08B456CEBB865DD2332CFF3FDF0D536E0BFEDC093211AC912899` | Backpack; conflicting laptop-compartment descriptions. |
| `product_info_10.pdf` | `0A86508992E36670F0F118F28F6F28BF3104BDFAD9B5A9ABCC803D12BA385BA0` | Pants; conflicting drying guidance and an unrelated review. |
| `product_info_11.pdf` | `D7260B629DDEDF4361FC7981B2D2802B4D1CC2FADDB408AE0BCAB9B657B8504A` | Shoes; placeholder warranty duration and incorrect return references. |
| `product_info_12.pdf` | `7B46C978FDA87DA9E422930461228703835FA39B73C5FBD5456655527721DB77` | Chair; inconsistent product category and weight; fictitious sample warranty. |
| `product_info_13.pdf` | `3E6104B7C6BEFA204C3DD57F143EAAD7CCFB5B872F2444227B14701B02A4E9C1` | Stove; fuel compatibility and safety claims need validation. |
| `product_info_14.pdf` | `7547D4F2FB3AE92A41952CA39D854BB629A7523106C835B81C87677A49AF78F7` | Sleeping bag; conflicting storage guidance and unclear temperature rating. |
| `product_info_15.pdf` | `1B9DDAA27CAC5469EB179DA4DD444E7DB6248E70B70E4116DB45C52996FBAA87` | Tent; generic product and purported contact claims. |
| `product_info_16.pdf` | `C89D8AAF7210E7C63D65CB5806870E19E92DE5B2F7DA810A88330A3D41AAEB08` | Daypack; load capacity appears only in the FAQ. |
| `product_info_17.pdf` | `A36191D39BDD9E2DC861B0CC3247E26BEF8BCF33C378A382DB02D4DBFD57DDF4` | Jacket; unverified rain protection and differing reviews. |
| `product_info_18.pdf` | `5A46295C20D344297C2656906F387123D0FD2C35475DD399CCB9BAAB6636B81C` | Sandals; potentially conflicting water-use guidance. |
| `product_info_19.pdf` | `57106F1318C38473672A2D26A5A5774E53ACABBFB6AED505FEEAE1AF0105CE24` | Table; unrelated stove reviews and an unsupported capacity claim. |
| `product_info_20.pdf` | `613B0417111E85285C35A01A673194D4DD9DFA8FBF5B776886B8E1F25A6F2D33` | Stove; conflicting fuel compatibility and questionable specifications. |

If a document is later cleared and approved, a low-risk retrieval check can
ask for the product name reported by that document and its source label. This
tests retrieval, not whether the product or its claims are real. Avoid using
conflicted specifications or safety instructions as expected facts. Do not
automate an inference request without separate approval.

Decisions about each document, exact uploaded bytes and retention, creation of
an agent version, switching the local agent, and live testing are separate.
No new sample manifest, uploaded corpus or agent version has been created.
