# B1764 Shipment and JSON totals against CPython

Lython `7f7c4ed59` and stock CPython 3.13.16 ran on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 26, 2026 UTC. Four workers
per phase contribute seven rotated batches each, with two workers per engine.
Ratios divide Lython elapsed time by CPython elapsed time and span the four
pairings of worker medians. Above 1× means Lython took longer.

| Workload | Input or source | Phase | Lython | CPython | Elapsed ratio |
| --- | ---: | --- | ---: | ---: | ---: |
| Shipment callback report | 2,048 records, 27,350 B | Warm sync | 1.83262–1.91124 ms | 1.65572–1.67061 ms | 1.097–1.154× |
| Shipment callback report | 2,048 records, 27,350 B | Warm `RunAsync` | 1.98308–2.04042 ms | 1.63465–1.64184 ms | 1.208–1.248× |
| Shipment callback report | 1,376 B of source | Compilation only | 0.06341–0.06400 ms | 0.11365–0.11404 ms | 0.556–0.563× |
| Shipment callback report | 1,376 B of source, 2,048 records | Compile-to-result | 2.09498–2.10661 ms | 1.90312–1.94081 ms | 1.079–1.107× |
| JSON service quantity totals | 2,048 records, 78,755 B | Warm sync | 0.66378–0.66619 ms | 0.48497–0.49106 ms | 1.352–1.374× |

Warm execution reuses compiled code in a running process. Elapsed includes
applicable RAM-backed I/O, verified output and files, and result disposal.
Lython uses an efficient mediated file host and shared admission. CPython uses
native file operations with ordinary cyclic GC. Compile-to-result uses
synchronous execution and disposes the compiled owner on each invocation.
Compile-only includes prior-owner cleanup. Startup, fixture reset and request
framing are outside the elapsed clock. Wider process CPU includes reset and
framing. Marked async uses Lython's `RunAsync` on the same synchronous guest
script as CPython.

Shipment remains 1.097–1.154× warm sync and 1.208–1.248× warm async.
Compilation is 0.556–0.563× and the combined phase is 1.079–1.107×.
The JSON totals include reading and decoding a complete document, native
field conversion and aggregated output. They take 1.352–1.374× warm elapsed
and 1.287–1.332× wider process CPU. The measured ratios describe the whole workload.
Use the separate source pair for attribution to the dictionary update.
[B1763 source comparison](../2026-11-26-converted-dictionary-update/README.md) ·
[Earlier external checkpoint](../2026-11-26-record-field-cpython/README.md).

Both independent goldens passed on the full 32-file frozen image. All 140
batches completed, both quiet gates passed, workers joined and guest
fixtures were removed. The persistent archive verified under the lease.

[Summary](summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
