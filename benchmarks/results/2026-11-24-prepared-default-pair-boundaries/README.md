# B1645 Reuse the proved boundaries of defaulted function pairs

The executable compiler already verifies both creation instructions and the
pair return. The prototype keeps the first creation index so each member can
read its existing function descriptor before admission. It removes separate
layout lookups and the mutable instruction index shared across the two members.
Default expressions still use the existing tape. Default storage admission,
arithmetic and promotion, annotation effects and callable admission retain
their order. Private stores and the return tail keep their cancellation spans.

The reference is `9d50a1b1f`, whose production source matches `b9b20aa98`.
Both frozen 32-file images satisfy all six CPython 3.13.2 goldens. Windows
and Ubuntu each pass 1,621 focused .NET 11 Release checks and 23 .NET 10
checks. Both execution APIs retain the terminal-refusal and cleanup tests.

| Workload | Phase | Elapsed change | Wider CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rules | Warm | −5.27 to −4.25% | −5.02 to −3.49% | 0.68 / 0.39% |
| Defaulted rules | Compile to result | −3.02 to −0.44% | −5.76 to +0.38% | 1.86 / 0.78% |
| Eager annotations | Warm | +1.69 to +7.53% | +0.51 to +7.96% | 5.14 / 0.57% |
| Postponed annotations | Warm | −2.37 to +3.01% | −1.83 to +2.30% | 4.72 / 0.76% |
| Captured rules | Warm | −1.33 to +1.46% | −0.93 to +1.30% | 0.75 / 2.05% |
| Quoted CSV invoice | Warm | −1.48 to +0.63% | −3.23 to +2.27% | 0.95 / 1.17% |
| Short Unicode report | Warm | −4.94 to +7.91% | −4.61 to +7.83% | 13.30 / 0.20% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Ranges span the four pairings of medians from two workers per
image. Workers warm for at least five seconds and supply seven rotated
batches each. Warm elapsed includes applicable RAM-backed I/O, output
checks and previous-result disposal. Time to result also includes compilation
and compiled-owner disposal. Process CPU covers the wider request including
framing and fixture reset. Startup is outside these clocks.

Defaulted rules use less warm elapsed and CPU. Their end-to-end CPU is
inconclusive. The eager control takes longer, with 5.14% reference-worker
spread. The new pair record adds eight CLR bytes per compiled code object.
Admitted peaks and retained compilation estimates match. The first variant
stays private. The bounded follow-up uses one existing discriminator field.

All 196 timed batches, both quiet gates, worker joins, fixture cleanup and
durable archive verification pass.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Execution allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Exact patch and procedures](procedures.tar.xz) · [Refinement](refined/README.md).
