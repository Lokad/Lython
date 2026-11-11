# R234/B574 — bounded constructor/text latency confirmation

2026-11-11, existing quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3. Five complete-script
source pairs finish in **142.323s**, four independent processes per job. Exact15f457f7
production/measured precommit core versus qualified4aa/frozen9ee7.
[All568 sources](source-identity.json),[fixed common images](identity.json).
This includes R232+R233; no individual-change attribution or new CPython ratio.

| Script / API | Elapsed change versus4aa | Wider process CPU change | Reference / current control spread |
| --- | --- | --- | --- |
| Named constructor CSV / sync | −5.94..−3.22% | −5.26% | 2.43% /0.39% |
| Named constructor CSV / async | −5.64..−2.22% | −11.42..0.00% | 2.29% /1.29% |
| Keyword-only constructor CSV / sync | −3.59..−0.19% | −6.25..+0.74% | 3.53% /0.00% |
| Captured-helper text / async | −0.22..+1.78% | −5.88..+1.64% | 1.35% /0.65% |
| Ordinary-helper text / async | **+1.92..+2.55%** | −4.80..+3.49% | 0.54% /0.07% |

Ranges span all four pairings of process medians, not confidence intervals.
Max within-process IQR1.15/2.13/1.43/0.91/1.30% respectively. Wider CPU is coarse
and covers the worker command envelope, not isolated guest execution.
[All medians/controls](summary.json). Keyword-only/captured-text results remain
inconclusive. Ordinary text's small elapsed regression is visible with tight
controls; no neutrality or CPU attribution claim. Larger desktop adversity is not
reproduced at the same magnitude; its original conditions remain preserved.

Retain the canonical proof/accounting correctness and shared binder, accepting
ordinary-text async2% latency as **explicit milestone debt**. Construction allocation
falls and real work disappears; preserve the independent admission fixes. Carry
this control into the next coherent transport design and decide final regression
debt before publication. No fixed percentage veto. This narrows uncertainty without
explaining all source/JIT/layout effects or completing the whole-script objective.
Allocation evidence is separately in [R232](../2026-11-11-default-allocator-admission/README.md)
and [R233](../2026-11-11-constructor-binding/README.md).

Normal GC/tiering/no overrides, identical source/fixtures/goldens;5swarmups and
7rotated approximately150ms batches. Warm includes public execution, mediated
RAM I/O, full checks and prior-result cleanup; excludes compile/startup/fixture
reset/protocol/final caches. Async host I/O completes immediately here. No pending-
I/O/RSS/embedding-tail claim. Separate R233 compilation has no CPU attribution here.

Loaded hashes/goldens/quiet-before+after/Closed/zeroexit/joins/RAM-removal/archive-
under-lease/localSHA gates all pass.210sowner/30stop/30margin; terminal cached,
never query again. Final free261537792B, fixed240MiBfloor/12MiBsetupheadroom.
[Terminal](terminal-status.json),[receipt](archive-receipt.json),
[raw source/cores/observations](observations.tgz),[archive identity](archive-identity.json).
Two obsolete6417/21cf inodes remain in a tracked canonical archive. Fresh lease,
path/UID/size/SHA/all-alias proofs precede retirement of five B506/B507 remote
aliases, now off limits. Protected9ee7/c271/9dadd unchanged. [Inventory](inventory.json).
Initial two-alias assumption refused before staging; full inode inventory found
the third alias. No source change, timed restart or relaxed assertion.

Corrections: local pairs use9a4272de; quiet uses separately fixedfec79/adapter build.
Do not combine cross-host clocks. Launched run.sh queried obsolete B573 for its
live-unit.txt, which is unusable. Actual B574210slimit/MainPID/active acknowledgement
and final success are independently verified in launch/terminal records. Originals,
initial setup failures and decisions survive in [preparation](preparation.tar.xz)
and [corrections](metadata-and-decision.json). Publication postponed.
