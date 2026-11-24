# B1647 Share executable default and creation descriptors

Default admission and callable creation refer to the same compiled function
descriptor. This removes one retained signature constant for each executable
definition with runtime defaults. Closed-definition analysis proves creation
eligibility before reading the defaults and validates the shared index at
creation. The runtime still admits storage in separate phases and evaluates
defaults and annotations between them. Both drivers share this representation.

The generated workload declares 96 functions and lambdas with computed or
literal positional defaults and computed keyword defaults. It calls the
factory 16 times and reads the resulting defaults. Retained constant entries
fall from 326 to 230, with matching opcode counts and code object counts.
Compilation allocates 4,888 fewer bytes, down 0.32%. Execution allocation and
peak admission match. Controls retain the same admission. Local execution
allocation is 120 bytes higher on eager rules and the manifest.

Both frozen 32-file images match four CPython 3.13.2 goldens. Windows and
Ubuntu each pass 921 white-box and 1,151 public .NET 11 Release checks and
59 focused .NET 10 checks. Tests cover default and decorator order, closure
and annotation behavior, suspension, refusal, and cleanup.

| Workload | Phase | Elapsed change | Wider CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated ordinary definitions | Compilation | −0.54 to +1.43% | −2.80 to +1.73% | 1.71 / 0.27% |
| Generated ordinary definitions | Warm | −0.77 to +2.57% | −1.51 to +2.61% | 2.35 / 0.99% |
| Generated ordinary definitions | Compile to result | −1.23 to +1.30% | −1.13 to +1.38% | 0.11 / 2.44% |
| Eager annotated rules | Warm | −1.62 to +13.25% | −3.58 to +14.07% | 7.62 / 6.96% |
| Defaulted rule pairs | Compile to result | −0.22 to +1.01% | −0.96 to +0.98% | 0.28 / 0.96% |
| Configuration manifest | Compile to result | −1.83 to −0.90% | −0.83 to +1.81% | 0.45 / 0.50% |

The reference is `654037855`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 24, 2026 UTC. Ranges span four pairings of
medians from two workers per image. Each worker warms for five seconds
and supplies seven rotated batches. Warm elapsed includes applicable
RAM-backed I/O, result verification and disposal. Time to result also
covers compilation and compiled-owner disposal. Wider CPU includes
fixture reset and protocol. Process startup is outside the clocks.

Keep the simpler representation and smaller constant pools. The generated
workload's timing is inconclusive in all phases. Both eager workers vary,
and that control's prior adverse result remains open. Manifest elapsed
falls in this collection with inconclusive CPU. All 168 timed batches,
quiet gates, worker joins, fixture cleanup and archive verification pass.

[Identity](identity.json) · [Constant census](constant-summary.json) ·
[Compile allocation](compile-allocation-summary.json) · [Execution allocation](allocation-summary.json) ·
[Timings](quiet-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [VM staging custody](retired-stage-custody.json) ·
[Exact patch and procedures](procedures.tar.xz).
