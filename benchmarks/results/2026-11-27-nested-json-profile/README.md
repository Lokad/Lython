# B1890 Attribute current nested JSON processing

The November 27 capture uses the delivered B1889 candidate at
`071a7d7a6`, .NET 11 RC3 and the original dense JSON scripts from
B1888. Ubuntu WSL is pinned to one logical CPU. The complete 32-file
image and independent CPython goldens verify. There are three six-second
managed thread-time captures after four seconds of verified warmup each.

| Workload | Verified commands during collection | JSON construction¹ | Record creation¹ | String decode¹ | UTF-8 copy or borrow¹ | Text file handling¹ |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Nested document, `Run` | 3,648 | 44.8% | 13.0% | 14.6% | 9.2% | 11.7% |
| Nested document, `RunAsync` | 2,944 | 38.1% | 8.4% | 9.9% | 5.5% | 11.0% |
| Nested Lines, `RunAsync` | 576 | 18.5% | 4.9% | 5.0% | 2.9% | 9.5% |

¹ Inclusive shares of time with resolved runtime ancestry after filtering
listed waits and GC-poll stacks. Regions overlap and inlining can hide
frames. These are managed thread-time observations. Use the matched
comparison for elapsed and process CPU.

Record construction and string allocation account for most of the
observed reservation samples in the document. The key-name lookup
region is under 1% in both document modes. The existing converted
dictionary update is the nearest runtime frame for 9.3–10.1% and the
governed byte builder's growth for 5.5–5.6%. These include useful execution
and work in callees. Inspect the file read and builder ownership boundary
for an intermediate copy or capacity transition before selecting a
redesign. Keep the earlier converted-update control debt visible.

All 112 batches verify and every worker and trace process joins. Raw
stacks and operation receipts archive with verified hashes.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Run summary](run-summary.json) · [Observations](observations.tar.xz) ·
[B1888 comparison](../2026-11-27-wsl-nested-json-cpython/README.md).
