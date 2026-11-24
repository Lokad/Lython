# B1567 fixed results in assignment order

Fixed tuple returns and exact fixed-width splits write directly into
the already admitted slots in target consumption order. Both call
drivers now skip the reversal. Cleanup still completes before any
target effect, and materialized split lists preserve field order.

| Complete warm script | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Captured rules | −3.30 to −1.81% | −3.44 to +0.26% |
| Default rules | −7.02 to +14.29% | −5.96 to +14.18% |
| Annotated rules | −9.13 to +6.77% | −9.95 to +6.39% |
| Quoted invoice | −1.43 to −0.80% | −1.25% |
| Expanded forwarding report | −2.26 to −0.58% | −1.58 to −0.63% |

Keep the removed pass. The two largest candidate worker spreads are
16.87% for default rules and 11.26% for annotated rules. Those timing
results remain inconclusive. Captured-rule elapsed improves with
mixed CPU. Peak admission matches on all five workloads, measured CLR
allocation differs by at most 124 bytes per invocation and each shared
allowance returns to baseline.

The reference `f9d600fe9` and candidate `95aa40601` images differ only
in the core DLL among 32 verified files. The candidate source was
merged as `8aef89b09`. The quiet Azure F4as_v7 runs Ubuntu 24.04.4 and
.NET 11 RC3 on November 24, 2026 UTC. Two workers per source warm for
eight seconds and contribute seven rotated samples. Ranges span the
four pairings of process medians. Elapsed includes mediated RAM-file
I/O where applicable, output verification and prior-result disposal.
Process CPU includes fixture reset and request overhead. All 140
batches match the CPython 3.13.16 goldens. Both quiet gates, worker joins,
fixture cleanup and persistent archive verification pass.

Windows and Ubuntu each pass 685 focused .NET 11 Release checks. The
first stage preflight named an incorrect reference directory. A second
attempt staged the exact images and stopped before fixtures or workers
because the collector still expected four lanes. The corrected launch
verified the same images and catalogs and sampled the intended five
lanes. The original launch conditions and failure receipts are archived.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures, failed launches and exact patch](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
