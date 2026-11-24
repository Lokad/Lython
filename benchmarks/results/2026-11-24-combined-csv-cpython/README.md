# B1549 CSV checkpoint against CPython

Lython `583d0e80b` includes prepared short dictionary keys and borrowed
regex replacement names. On November 24, 2026 UTC, it ran these CSV
scripts against stock CPython 3.13.16 on the Azure F4as_v7 quiet VM with
Ubuntu 24.04.4 and .NET 11 RC3. Each file workload reads 4,096 records.

| Script and phase | Lython elapsed | CPython elapsed | Lython / CPython | Worker spread, Lython / CPython |
| --- | ---: | ---: | ---: | ---: |
| Explicit `float()`, warm | 2.93047–2.95379 ms | 2.81272–2.82650 ms | 1.037–1.050× | 0.80 / 0.49% |
| Explicit `float()`, compile to result | 2.96892–3.05676 ms | 3.03241–3.05258 ms | 0.973–1.008× | 2.96 / 0.67% |
| Explicit `float()`, compilation only | 0.04463–0.04508 ms | 0.07185–0.07191 ms | 0.621–0.627× | 1.00 / 0.09% |
| Typed Unicode, warm | 3.70511–3.78527 ms | 3.10224–3.16787 ms | 1.170–1.220× | 2.16 / 2.12% |
| Quote-heavy invoice, warm | 6.22608–6.31403 ms | 6.29671–6.39429 ms | 0.974–1.003× | 1.41 / 1.55% |

The ratios divide Lython elapsed by CPython elapsed and span all four
pairings of independent worker medians. Warm execution reuses compiled
code. The execution clocks include applicable RAM-backed I/O, output
verification and prior-result disposal. Lython uses the mediated host
and active shared admission. Compile-to-result adds compilation and
compiled-owner disposal. Compile-only includes that disposal. Fixture
reset, protocol, startup and final process teardown sit outside elapsed.
Wider process CPU is reported separately. These are diagnostic timings.

The explicit meter is within 5.1% warm and spans parity through result.
The quoted invoice also spans warm parity. The same-Lython-source
comparison still records a small invoice elapsed penalty with mixed CPU.
Typed Unicode has a clear warm gap. Earlier phase observations remain
archived at their own revisions. All 140 batches matched their goldens
and both machine gates passed. The workers joined, RAM fixtures were
removed, and the persistent archive verified under its lease.
[Matched Lython comparison](../2026-11-24-short-dictionary-keys/README.md) ·
[Earlier CSV checkpoint](../2026-11-24-current-csv-cpython/README.md).

## Allocation attribution

Separate Windows x64 .NET 11 RC3 traces use the same Lython image for
the two meters and an expanded-option JSON Lines export. Each warmed
worker has six seconds of EventPipe allocation sampling. Across 38
sampled batches the workers complete 480 explicit meter, 480 typed
meter and 256 JSON invocations with checked outputs. Allocation tick
weight approximates where the wider request allocates. It does not
count objects or estimate CPU.

| Workload | Largest sampled work |
| --- | --- |
| Explicit meter | 61.84% nearest `PyTuple.FromSlots` across double boxes, backing arrays and shells. |
| Typed Unicode meter | 23.32% in boxed doubles at CSV unpack and 38.07% across the tuple boxes, backing and shells. |
| Expanded JSON Lines export | 21.87% in the byte builder's growth and 19.05% in option binding, Boolean boxing and option objects. |

[Runtime and input identities](identity.json) · [Timing summary](summary.json) ·
[Raw timing](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Allocation sites](allocation-summary.json) ·
[Procedures and allocation traces](procedures-and-allocation-traces.tar.xz).
