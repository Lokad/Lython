# B1731 CPython comparison after direct scalar arguments

The retained `a420c3e05` source runs against stock CPython 3.13.16
on the quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3 on
November 25, 2026 UTC. The same 32-file candidate image completed the
[B1730c paired comparison](../2026-11-25-arithmetic-call-operands/confirmation/README.md).
Both complete script goldens passed on the local images and on the VM.
The ordinary factory creates 96 definitions 16 times. Shipment reads
2,048 records from a 27,350 B CSV and emits a verified report.

| Workload | Phase | Lython elapsed | CPython elapsed | Lython / CPython | Same-engine worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary definitions | Compilation only | 0.57053–0.57267 ms | 1.10525–1.11168 ms | 0.513–0.518× | 0.37% / 0.58% |
| Ordinary definitions | Warm sync | 0.25959–0.26377 ms | 0.14653–0.14787 ms | 1.756–1.800× | 1.61% / 0.91% |
| Ordinary definitions | Compile-to-result | 0.83433–0.84990 ms | 1.11491–1.12865 ms | 0.739–0.762× | 1.87% / 1.23% |
| Shipment callbacks | Warm sync | 2.27304–2.39573 ms | 1.62761–1.63701 ms | 1.389–1.472× | 5.40% / 0.58% |

Ratios span all four pairings of engine process medians. Values above
1× mean Lython takes longer. Each mode uses two processes per engine, five
seconds of warmup per process and seven rotated batches per process.
These are diagnostic observations. Warm execution includes applicable
RAM-backed I/O, result verification and disposal. Compilation includes
compiled-owner disposal. Time through result combines compilation,
execution and both cleanups. Fixture reset, framing and process startup
sit outside elapsed. Raw wider process CPU includes framing and reset.

The first launch stopped before any workers or fixtures because the
manifest omitted the second Lython worker identity. The corrected launch
used the same measured image and the same workloads and phases. All workers
joined, both quiet gates passed and the archive was checked under the lease.

The retained shipment tape gives `choose` a direct return with four
instructions. `tally` has 73 instructions, 21 value slots and two eager
list comprehensions. Each uses a binding context, generic append and
context exit. This is a source-level inventory. A future design must
remove the binding or handoff stage and establish its effect with a new
paired comparison.

[Summary](summary.json) · [Image identity](identity.json) ·
[Shipment instruction shape](shipment-shape.json) · [Raw observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures and failed launch](procedures.tar.xz).
