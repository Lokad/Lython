# B1609 Cased text helpers against CPython

The frozen 32-file image at `46c967257` matches the runtime and tests
at `675900142`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4, .NET 11 RC3
and stock CPython 3.13.16 on November 24, 2026 UTC. Two workers per engine
and lane each run seven rotated batches after eight seconds of warmup.
Ratios span all pairings of process medians and divide Lython time by
CPython time. Above 1× means Lython takes longer.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Short Unicode report | Warm | 0.89693–0.97136 ms | 0.93726–0.94026 ms | 0.954–1.036× | 8.30 / 0.32% |
| Generated rules with defaults | Warm | 0.96412–0.96583 ms | 0.68102–0.70088 ms | 1.376–1.418× | 0.18 / 2.92% |
| Short Unicode report | Compilation and disposal | 0.02881–0.02892 ms | 0.05577–0.05597 ms | 0.515–0.518× | 0.37 / 0.35% |
| Short Unicode report | Compile to result | 0.98993–0.99296 ms | 1.07912–1.08041 ms | 0.916–0.920× | 0.31 / 0.12% |

The report reads a 35,840-byte, 3,072-line RAM-backed file, writes the
expected report and captures stdout. Lython uses mediated ranged I/O.
The rules script constructs and applies 2,048 groups and captures stdout.
Warm elapsed includes applicable I/O, result verification and disposal
of the previous result. Compile to result includes compilation, one run
and cleanup. Compilation includes disposal of the compiled owner. Process
CPU also includes fixture reset and request framing. Launch and final
teardown sit outside all lanes.

Short-report warm ratios straddle parity with 8.30% Lython worker spread.
Compile-to-result worker medians agree more closely and place this report
about 8% ahead of CPython. Defaulted rules remain slower warm with wider
process CPU in the same direction. Both goldens and all 112 timed batches,
quiet gates, worker joins, fixture cleanup and archive verification pass.

A separate untimed Windows diagnostic on this source records 4,096
64-byte singleton positional-default tuples per defaulted invocation.
None of the four verified invocations reads `__defaults__`. The quote
control uses neither operation. This accounts for 262,144 bytes per
defaulted invocation, about 26.8% of the local warm allocation observed
in B1608. The census is evidence of storage to investigate. Its diagnostic
worker is separate from the frozen timing workers.

[Timings and CPU](summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures and allocation census](procedures.tar.xz) ·
[Terminal status](terminal-status.json) ·
[B1608 within Lython](../2026-11-24-closed-line-helper/README.md) ·
[Earlier CPython comparison](../2026-11-24-closed-pairs-cpython/README.md).
