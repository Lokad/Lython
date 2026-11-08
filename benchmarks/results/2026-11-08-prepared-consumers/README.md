# Prepared member, iterator and branch operands

Baseline production `913ea550`, core `18b563e9`. Candidate is `71445454` plus
the preserved five-file runtime patch, core
`4c072fed1badf89afc3d72164c88a5488a90b3a85c2a7ee6fd6ff7b983b71570`.
Its informational SourceLink predates the commit; the complete production
inventory identifies measured source. Workers and optional 0.4 adapter are unchanged.

Retain completion of the existing prepared-input representation: adjacent pure
local/constant loads no longer dispatch/publish temporary inputs before member
access, iterator acquisition or truth branches. Captured/mirrored sources keep
ordinary lookup. Physical outputs retain snapshots; actual protocols still promote
and suspend. No new argument view, arena, dispatch framework, cache, owner or option.
Native helper proofs stay unchanged. Static instruction reductions are not CPU
shares: text module77→74, CSV83→79/helper17→16, report62→58. Arithmetic control
instructions are identical. Allocation is neutral.

B445: six complete jobs,167.554seconds, quiet Ubuntu F4as_v7/.NET11RC3, normal
GC/tiering, two baseline/two candidate processes,5s warmup/role/job and seven
rotated150ms rounds. Ranges cover all four old/new median pairings:

| Workload/API | Candidate elapsed change | Interpretation |
| --- | ---: | --- |
| Rsplit-helper text, sync | −1.85 to−0.13% | Modestly lower elapsed; coarseCPU−1.67to+6.67%, no CPU-win claim |
| Rsplit-helper text, async | −4.74 to−0.98% | Lower observed elapsed; old spread3.60% limits magnitude |
| Guarded text, sync | −0.77 to+1.76% | Near baseline |
| CSV helper, sync | −0.24 to+1.50% | Near baseline; possible regression retained explicitly |
| Report, async | −4.02 to+2.15% | Inconclusive, candidate spread5.32% |
| Arithmetic control, sync | −1.53 to+0.92% | Near baseline |

The retention decision is architectural consistency and removed execution work,
not demonstrated substantial CPU improvement. Do not extend this into another
series of per-opcode tweaks. Earlier R76float/R79CSV debt remains separate.
Original adverse desktop results, unstable unchanged controls and partial snapshots
from a stale-control-metadata failure remain. The initial six-case harness rejection
and wrong immutable launcher-name failure also remain; no runtime golden was relaxed.

332internal/155public focused Release checks pass per actual .NET11/.NET10.
Public tests exercise genuinely suspended truth/member/iterator protocols, in-flight
cancellation, awaited cleanup, budget reuse and original unbound-read spans, alongside
existing closure, terminal-refusal and native-iteration tests. Committed91f15a3e is fully qualified: [cached CI42451722109](delivery-ci.json),
Windows/Ubuntu actual .NET11/.NET10, originalStress and installed core consumers.
This qualifies that runtime source, not subsequent changes.

Warm timing includes public invocation, mediated RAM I/O, full stdout/files validation
and prior-result cleanup. Compilation/startup/staging/reset/protocol/final cache
teardown are excluded. Actual suspension is tested separately. No CPython, universal
CPU, RSS or embedding-tail claim. All exact source/core/auxiliary/actualReady/runtime/
golden/quiet/Closed/exit/join/fixture/archive gates pass. Workers join and owned RAM
fixtures are removed/unmounted before archival under lease and independent local
verification.210s owner+30s stop+30s margin; no new VM data deletion/cleanup.

[Assessment](assessment.json) and [complete evidence](observations.tar.xz) preserve
both cores, source inventory/patch, census, all original observations, setup failures,
independent CPython goldens, both-target test logs and the verified VM archive.
