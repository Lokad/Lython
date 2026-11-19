# B1016 forwarding and rule closures against CPython

Measured `faeec7e81` with frozen core `9edeede4` on 2026-11-18 UTC, against
stock CPython 3.13.16 (PGO/LTO, GIL, no JIT). The quiet Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3. The Lython source and tests match the
[fully qualified R587 checkpoint](../2026-11-18-trailing-keyword-operands/delivery/README.md).
The subsequent compiler fact change has its own
[paired comparison](../2026-11-18-abstract-value-facts/README.md).

| Script | Lython / CPython compilation | Warm | Compile-to-result |
| --- | ---: | ---: | ---: |
| Expanded forwarding report | 0.87–0.88× | 1.86–1.87× | 1.74–1.75× |
| Unannotated rule closures | 0.80–0.81× | 4.24–4.48× | 4.15–4.33× |
| Annotated rule closures | 0.82–0.84× | 5.40–5.64× | 5.27–5.31× |

Each range combines the two process medians per engine. Two processes per
engine alternated all three phases for five rounds per process and phase.
All 180 timed responses passed the output checks, both quiet gates passed
and the workers joined before cleanup. The collection took 188.04 seconds.
Unannotated warm control spread reached 3.29% in Lython and 2.25% in
CPython. The ratios are diagnostic, and source-change attribution uses
the paired Lython comparisons.

Warm elapsed includes mediated RAM-backed I/O, output verification and
previous-result cleanup. Compilation includes compiled-owner disposal.
Compile-to-result includes compilation, execution and compiled-owner
disposal. Startup, fixture reset, framing and final cache teardown are
outside guest elapsed. Process CPU covers a wider request boundary.
CPython uses native filesystem APIs.

[Elapsed, CPU and control spreads](summary.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Terminal status](terminal-status.json) ·
[Earlier checkpoint](../2026-11-18-post-transport-cpython-checkpoint/README.md)
