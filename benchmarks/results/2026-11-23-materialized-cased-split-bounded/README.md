# B1436 Bounded scratch for materialized cased words

`9fb7fdcfb` retains the measured implementation from `50d3a8428`.
The materialized path shares the immediate iterator's casing and
word-boundary scan. It admits the final list and independent words
together. Short inputs use stack scratch. Middle-sized inputs borrow the
existing execution word buffer. New packed capacity is limited to 16 KiB
for this path. Longer inputs use the existing sizing and direct word
construction. The immediate iterator skips the materialized word count.

The reference is `f53c32e53` with the exact B1431 core. Only the core differs
across the two verified 32-file images. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on November 23, 2026 UTC. Catalog, inputs and worker
configuration match B1435. The starting worker order is reversed.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Short Unicode materialized report | Warm | −10.28 to −9.01% | −10.92 to −8.76% | 0.67 / 0.74% |
| Short Unicode materialized report | Compile to result | −12.37 to −7.21% | −12.61 to −6.20% | 5.85 / 0.03% |
| Wide materialized report | Warm | −19.64 to −18.68% | −18.06 to −17.26% | 0.35 / 0.85% |
| ASCII materialized report | Warm | −12.60 to −8.03% | −12.48 to −8.39% | 0.13 / 5.10% |
| Immediate nonlocal report | Warm | −4.77 to −2.25% | −4.19 to −2.47% | 0.84 / 1.79% |
| Dense prepared histogram | Warm | −5.66 to +1.33% | −4.98 to +1.40% | 6.28 / 1.07% |
| Ordinary split report | Warm | +1.83 to +7.36% | +1.48 to +6.94% | 1.58 / 3.79% |
| Quote-heavy CSV invoice | Warm | −4.45 to −0.88% | −4.44 to −0.41% | 2.87 / 0.84% |

Ranges span pairings of medians from two independent workers per source.
Each worker warms for eight seconds and supplies seven rotated batches.
Warm elapsed includes mediated RAM-backed I/O, verification and previous-result
disposal. Compile to result includes compilation and compiled-owner disposal.
Wider CPU includes resets and protocol. Startup and final teardown are outside
elapsed. CPython supplies the semantic goldens.

Wide-report allocation falls from 867,640 to 824,184 bytes in the local
diagnostic. Admitted peak falls from 1,508,761 to 1,475,125 bytes. The separate
132 KiB input matches at 680,128 allocated bytes and 410,770 admitted at peak.
The earlier pooled version more than doubled its admitted peak. Every local
allowance returns to baseline.

Retain the bounded version. The warm and first-result target gains recur
and the long-input storage cost returns to baseline. Ordinary split takes
longer even though its implementation is unchanged. Its timing spanned
zero in B1435 and the new observation remains unexplained. Carry the paired
control into the next integrated checkpoint. The higher quoted-invoice
timing in B1435 does not recur.

Windows .NET 11 and Ubuntu .NET 10 each pass 694 focused Release checks
and four original-scale text construction rollback cases. Both images
match seven complete CPython goldens, 134 Unicode inputs, 24 inputs around
the stack and buffer transitions, and the long-input result. The
prepared-buffer cancellation regression reproduces on the reference.
The materialized tests cover cancellation and refusal during scratch refund.
The quiet run verifies 224 batches in 439 seconds. Both quiet gates pass,
workers join, guest fixtures are removed and the archive verifies under
the lease. The only retired working core is the byte-verified B1435 image.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json) ·
[B1435 initial comparison](../2026-11-23-materialized-cased-split/README.md).
