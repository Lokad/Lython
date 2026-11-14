# Initial literal-tuple prototype: correction required

R351,2026-11-14: qualifiede235b18b/frozen71ba421c versus initial01fd9be0.
The existing builder recognizes a complete same-block run of immutable constants
and replaces repeated child loads/MakeTuple with one compiled tuple. Dynamic,
mutable, starred and side-effecting expressions retain genuine construction.
No second syntax pass, evaluator, cache, ledger or memory owner is introduced.

Initial135internal/476public per actual11/10 pass (1,222total), including11isolated
Python controls and retained results. Six original probe-launch failures are
preserved: system dotnet lacks net11; unique pinned child PATH/runtime fixes them
without source/assertion changes. Four fresh allocation processes show invoice
−7.51%, fulfillment−4.68%, captured-text control flat. Constant-return/unpacking
allocation is effectively flat; dynamic-return control exactly flat. These are
allocation observations, not CPU gains or a final retention decision.

The initial representation is **not suitable for promotion**: new unowned compiled
tuples expose legacy source-owner allocation in slicing/concatenation. Six focused
regressions reproduce missing refusal/cancellation and absent result ownership.
The correction uses current execution funding through the existing known-count
constructor, deleting source-governor selection/TupleLikeOwnership, unowned concat
and LINQ/growth materialization. ZIP's fixed six-item consumer stops copying the
whole input before validating length. Two unchanged allocation guards fail exact
01fd at34–36KB versus16KBguard and pass the correction. This is a bounded migration
fix for actual consumers, not guest-heap tracing or a general ownership audit.

B722 ran invoice warm/compile/compile-run and fulfillment warm, then its new
constant-return diagnostic failed startup: fixture size/text metadata were missing.
Initial catalog and all failures are preserved. Corrected metadata passes actual
frozen worker Ready/output/file/budget verification locally before another VM run.
The failed owner is terminal, all users join, and the empty RAM mount is archived/
verified before unmount. No restart. Raw failed SHA256:
`9c001d21fd995d649d6db79892b6a9e8118dbea89ebb0265808fbe957888c29c`.

| Completed phase, original01fd only | Elapsed source-pair change | Wider CPU change |
| --- | ---: | ---: |
| Invoice warm | −1.40..+0.46% | −0.76..+1.00% |
| Invoice compilation | −1.23..+0.93% | −0.30..+0.22% |
| Invoice compile-to-result | −5.25..+1.78% | −4.62..+2.62% |
| Fulfillment warm | −1.95..−0.44% | −2.71..−0.87% |

**Diagnostic partial evidence:** final quiet and control jobs are missing; the
source also requires the ownership correction. No final speedup or neutrality
claim follows. Twelve-second full-request warmups/five1s rotating batches/four
fresh processes per job on the existing quiet F4as_v7 Ubuntu24.04.4/.NET11RC3 VM,
ordinary GC/tiering. Warm includes mediated RAM I/O, output/file checks and prior
result cleanup; excludes compilation/startup, reset/protocol and final cache
teardown. Wider CPU includes reset/protocol. No CPython timing or embedding-tail
claim. All adverse medians and original failed conditions remain immutable.

Before staging, one superseded53core (six proven aliases) and three duplicate raw
archives were retired after exact canonical Git/SHA/path/UID/terminal/all-alias
proof. Current71ba/8ae images, catalogs, toolchain and254/252MiBfloors survived.
Corrected source has separate frozen bits/focused qualification and must receive
its own final comparison. Publication/tagging remain held.

[Ownership correction](ownership-correction.json),[partial timing](partial-timing-summary.json),
[complete original sources/cores/tools/raw checks and failures](sources-checks-and-failed-observations.tar.xz).
