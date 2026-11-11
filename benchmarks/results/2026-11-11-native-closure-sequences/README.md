# Captured helper sequences — 2026-11-11

Retained `6904093e` versus qualified `5f9059fa`. The existing bounded native-body
proof and evaluator now support live incoming cells and proven numeric nonlocal
stores followed by a closed continuation. Complete captured text helpers omit
binding, workspace and general instruction-loop stages. Cell references occupy
the existing managed inline input buffer after actual parameters; immutable
compiler metadata separates these domains. Owner/mirror writes, source spans,
exact promotion and dynamic guards remain. Wide or unproven calls use ordinary
execution, selected before effects. No new execution route, owner or option.

## Results and decision

Runtime CLR allocation is effectively **flat**. This is distinct from the earlier
19–20% allocation reduction in native helper returns. Compilation allocation rises
0.93% for captured text and 0.39% for CSV.

| Quiet VM workload | Elapsed change | Wider process CPU change | Decision / limit |
| --- | ---: | ---: | --- |
| Captured text / Run | −15.17..−10.56% | −13.79..−12.79% | Supports retained phase removal |
| Captured text / RunAsync | −18.16..−16.59% | −15.00..−11.46% | Supports retained phase removal |
| Ordinary text / Run | −4.47..+2.02% | −3.64..+1.83% | Inconclusive |
| Ordinary CSV / Run | +0.49..+0.89% | 0..+6.67% | Small adverse control debt; CPU coarse |

Ranges cover all four pairings of independent process medians, not confidence
intervals. Negative means faster. Accept the explicit ordinary-text uncertainty,
small CSV control regression and modest compilation-allocation debt at this
milestone. Reassess material debt at a coherent checkpoint before publication.
No universal CPU gain, neutrality or pending-I/O latency claim.

The initial extra-cell-parameter implementation slowed ordinary desktop text
14–20%, with a tight async unchanged control. Its exact source and all adverse
data survive. A source-supported refinement removes ten shared signature
parameters and all 41 forwarding sites, reusing existing input storage. The
ordinary slowdown does not recur, without establishing its cause. Refined desktop
captured timings improve, but CSV sync remains +10.29..+17.73% and report async
has a 24.27% unchanged control. Quiet results do not erase these observations.

## Boundary and acceptance

The existing Windows complete-job driver uses fixed common `9a4272de` worker and
adapter images, three isolated cores sharing CLR/GC, 256 warmups and five rotating
rounds of sixteen complete jobs. Both APIs and compilation are separate. Complete
jobs include fixture reset, mediated I/O, full output checks and result disposal.
All six scripts pass goldens, balances and cleanup. Desktop clocks are diagnostic.

B583 uses the existing F4as_v7 Ubuntu 24.04.4 quiet VM, four AMD EPYC 9V45 vCPUs,
16 GiB/no swap, .NET 11 RC3 and fixed `fec79` worker. Four jobs finish in 114.077 s,
with five-second warmups and seven rotated 150 ms rounds. Warm includes mediated
RAM I/O, full validation and prior-result cleanup; compilation, startup, reset,
protocol and final caches are excluded. External CPU includes protocol/reset.
No desktop/VM clock merging or new CPython ratio.

4,742 final focused Release checks pass across actual .NET 11/10, plus ten
independent CPython 3.13.2 correctness goldens. Regressions cover receiver/named/
wide calls, missing-parameter versus cell isolation, live rebinding/deletion,
protocol fallback exactly once, promotion, terminal refusal after the first cell
write, original failure location despite suppression/finally return, retained
aliases and genuinely pending protocol cancellation with joined cleanup and
shared allowance returning to baseline. Full exact-source platform/stress/
installed-consumer delivery is separately tracked by run 43058223560; focused
checks do not establish it.

## Evidence

[Decision](decision.json), [archive hashes](archive-identity.json),
[initial assessment](initial/initial-assessment.json),
[refined identity](refined/diagnostic-identity.json),
[quiet summary](quiet/summary.json) and [raw quiet archive](quiet/observations.tgz)
preserve both implementations, all controls and failed preparation conditions.
Each form includes exact source snapshots, compressed actual11/10 core images,
local observations and regression logs. Frozen refined source hashes match the
retained production source. The quiet qualified reference differs from the
desktop fresh baseline only in recorded metadata/comment; clocks stay separate.

Quiet hashes, golden results, ownership, machine gates, joined users, RAM cleanup
and archived bytes all pass. B583 is terminal/cached. VM disk recovery removed
only obsolete core aliases and one duplicate historical archive after fresh
UID/inode/all-alias checks against exact committed canonical Git evidence; current
roles were protected. All original evidence remains available.

This milestone does not complete the performance objective. External CPython rows
remain labelled with their earlier measured source until the coherent refresh;
regex development remains independent and publication is postponed.

Subsequent exact-`6904093e` full delivery is **GREEN** on Windows/Ubuntu, actual
.NET 11/10, original stress and installed Release consumers. The
[terminal receipt](delivery-ci-status.json) is cached. Original collection-time
status and adverse observations remain unchanged; this qualifies the retained
source, not later prototypes.
