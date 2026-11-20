# B1213 Configuration scripts against CPython

Measures production `645a0fcd1` using the source-matched `356a93e25`
benchmark image and stock CPython 3.13.16. The quiet Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Each workload used two
processes per engine and five rotated rounds targeting 0.4-second batches.
Ranges cross all four process-median pairings. The ratio is Lython elapsed
divided by CPython elapsed.

| Workload | Input | Boundary | Lython | CPython | Ratio | Duplicate elapsed spreads, Lython / CPython |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| Generated validator | 96 guards / 1,714 B JSON | Warm | 0.08372–0.08402 ms | 0.04481–0.04509 ms | 1.86–1.88× | 0.36% / 0.62% |
| Generated validator | Same input | Compilation only | 1.63857–1.66815 ms | 1.87953–1.91107 ms | 0.857–0.888× | 1.81% / 1.68% |
| Generated validator | Same input | Compile-to-result | 1.763–1.794 ms | 2.046–2.086 ms | 0.845–0.877× | 1.75% / 1.98% |
| Configuration manifest | 4 targets / 833 B across 2 files | Warm | 0.19391–0.19578 ms | 0.15837–0.15876 ms | 1.22–1.24× | 0.96% / 0.25% |

Warm includes mediated RAM-backed I/O, output and file golden checks,
and prior-result cleanup. Compile-to-result includes compilation, one
execution and compiled-owner cleanup. Compilation alone includes
compiled-owner cleanup. Fixture reset, process startup and final teardown
are outside the guest clocks. Wider process CPU is recorded separately.
Lython runs with an unlimited shared allowance and active accounting.
CPython uses its native file APIs and ordinary runtime.

The Lython validator reaches its first result sooner in this collection
and takes nearly twice as long when both engines run already compiled
code. The manifest warm control stays near its earlier ratio. Historical
validator and manifest runs have their original revisions and conditions.
Use [B1212](../2026-11-20-shared-executable-graph/README.md) to attribute
changes to the compiler simplification. The external comparison is
diagnostic and does not isolate a new cause for the warm gap.

Both quiet gates and all 80 batches passed in 144 seconds of collection.
Source and complete image identity, stock CPython identity, exact goldens
and archive hashes verified. All workers joined, the persistent archive
verified under lease, and the guest fixtures were removed.

[Timings and controls](summary.json) · [Raw observations](observations.tgz) ·
[Source and procedures](procedures.tar.xz) ·
[Prior validator checkpoint](../2026-11-20-cpython-reports-validator/README.md).
