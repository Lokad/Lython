# B1140 CSV row-finalization CPU attribution

The verified `e20604ef4` image ran on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026 UTC. This is the
same warm CSV runtime as `3c487577c`. The intervening production changes
were in the frontend, and the measured scripts use neither regex nor
supplementary-character literal sequence analysis. Both quiet gates
passed. All three scripts matched the CPython output and file goldens.
The VM verified the persistent archive before removing guest fixtures.

| Observed stacks | Invoice | Keyword invoice | Long-field validation |
| --- | ---: | ---: | ---: |
| With Lython runtime ancestry | 1,411 | 1,513 | 1,366 |
| Source pull or parser feed in ancestry | 287 | 248 | 402 |
| Field or row finalization in ancestry | 104 | 85 | 111 |
| Record layout proof or adoption in ancestry | 48 | 39 | 57 |
| Parser feed as nearest Lython runtime frame | 111 | 90 | 150 |
| Row finalization as nearest Lython runtime frame | 68 | 61 | 70 |

Categories overlap. These samples include useful work and inlined calls
and give no estimate of removable CPU. The earlier allocation trace shows
that finalization must still create distinct guest string values. Row
adoption already reuses the admitted value array and preserves header
identity across reentrant admission. The profile supports testing whether
preparing immutable delimiter/quote search sets at reader construction can
remove the per-line scanner setup while keeping a single parser and
bounded cancellation checks.

The first local preparation used a historical workload alias that was
absent from the catalog and stopped before staging. The corrected run
used the exact long-field validation case. Only a verified redundant
working image from B1138 was retired to make room for the trace.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
