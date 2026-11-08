# Shared native method and compiler return facts

R89,2026-11-08. Retained source isaaa9cb09 plus the preserved patch; exact source
inventory/core are in [assessment](assessment.json). Baseline7796a24f/corecc6b1b15
is fully qualified by cached CI42459221409. Optional adapter/worker unchanged.

Closed zero-argument string calls now prepare their canonical ReceiverMethod and
reuse existing frontend return facts. Removed: method-specific native-return enum
entries, GetNoArgumentsKind/map, enum-indexed declaration array/accessors and
duplicated guard/evaluation branches. Six missing Boolean facts join the existing
catalog. Prepared method references belong to native value nodes, with four
generic result shapes; no new per-name proof flags, type catalog, engine, owner or
pool. Existing lower/upper whitespace fusion directly references its original
receiver, removing the dead casing node/result proof/empty argument scan while
preserving original member/call span-check order. Argument-bearing proofs remain.
Other runtime types use ordinary execution before effects; no retry after work.

397internal/2165public focused Release checks pass per actual.NET11/.NET10,
including three independent CPython cases for Unicode compositions, custom
receivers, sibling fused results and forwarded locals. Existing cancellation,
terminal admission, aliases, real async protocols and cleanup assertions remain.
Final short source pairs have neutral allocation and full golden/funding/isolation/
cleanup checks. Desktop elapsed is diagnostic; all adverse/unstable controls remain.

B451: six jobs168.406s on quiet Ubuntu F4as_v7/.NET11RC3, normal GC/tiering,
two old/two current processes, five-second warmup/role/job, seven rotated150ms
rounds. Ranges cover all four process-median pairings:

| Complete script/API | Elapsed change | Interpretation |
| --- | ---: | --- |
| Rsplit helper, sync | −17.85 to−13.94% | CoarseCPU−18.31to−13.04% agrees |
| Rsplit helper, async | −19.25 to−16.06% | CoarseCPU−18.18to−11.46% agrees |
| Casefold helper, sync | −14.17 to−11.14% | CoarseCPU−15.25to−10.53% agrees |
| Existing fused helper control, sync | −3.77 to+2.31% | Inconclusive; old/current spread3.03/3.19% |
| CSV control, sync | +0.42 to+1.03% | Small possible regression accepted; coarseCPUflat |
| Report control, async | −1.47 to+2.93% | Inconclusive; old spread3.27% |

Initial call-site-metadata prototype and adverse desktop observations are retained.
The node-owned refinement B450167.945s had affected elapsed13–18%lower but fused
control+2.52–8.29%, CPUflat-to-adverse. Removing its dead producer work justified
one refined confirmation. The concern is not reproduced in B451; separate windows
do not prove its full cause. No unchanged favorable retiming or discarded control.
Earlier regression debts remain separate; this milestone does not finish the
whole-script performance objective.

Timing includes invocation, mediated RAM I/O, full validation and prior-result
cleanup; excludes compilation/startup/staging/reset/protocol/final cached teardown.
I/O completes synchronously in timing; actual suspension is tested separately.
No refreshed CPython ratio, universal CPU, RSS or embedding-tail claim. Full final
Windows/Ubuntu actual11/10/originalStress/installed qualification follows commit.

All source/core/auxiliary/actualReady/runtime/golden/quiet/Closed/exit/join/fixture/
archive gates pass. Both VM collections are terminal, archived under lease and
independently verified locally; workers joined, owned RAM fixtures removed/unmounted.
210s owner+30stop+30margin. B451 reuses the exact verified B450 baseline hardlink;
all original data/cores/paths remain. No new deduplication or evidence deletion.

[Complete evidence](observations.tar.xz) preserves all sources/cores, initial
conditions, three semantic goldens, target logs and original/refined observations.
