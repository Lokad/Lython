# Source correction and measurement uncertainty

B346 completed all 12 quiet/identity/golden/Closed/join/fixture gates in228.43s.
Archive SHA2568c5d5ba696ebbc423aaf9bf3a5a7b939c4ef7d68d7f806ed93bf93da28d0aea3.
The frozen prototype91628fe2 predates a full-suite slice-context correction. These
workloads passed their complete goldens, but this is not final-source qualification.

The full local suite passed4,259internal and16,185public, failing one existing
SubscriptSliceBounds_CoerceIndexLikeCpython case. Shared dispatch lacked the null-
invocation guard on Slice (an insertion script duplicated GetIter's guard instead).
Corrected the existing Slice case before any effects; removed the duplicate guard.
Added a public scalar-prefix/late-__index__ slice golden through both APIs. Original
logs/build failures stay in focused-checks; no supported behavior was removed.

CSV warm elapsed−3.93%/−1.72%, fresh−1.58%/−3.50% versus qualifiedR10. Adverse text
warm+4.80%/+2.84%, JSON warm+5.77%/+3.49%, report warm+2.80%/+5.00% remain visible.
Text fresh elapsed improves while process CPU is adverse; JSON compile identical
candidate/control diverge18.17% despite unchanged compiler source. Three-second
warmups and seven150ms rounds are insufficient for a confident broad conclusion.
This establishes uncertainty/regression debt, not a failed architecture or CPU gain.

Source inspection also found that the first no-growth write proof did a bucket
lookup and then repeated it in SetItem when a full table already contained the
key. Replace that predicate with one admitted-storage-only try-write, sharing
PublishNativeValue with ordinary writes. A false result has no guest effect or
admission; growth still materializes before reservation. No second dictionary
algorithm or retry is introduced. The existing admission-state test covers it.

Bounded correction: qualify the Slice source fix and shared write, then confirm the coherent source
pair using the established five-second warmup, same four scripts/three boundaries,
seven rotating rounds, identical candidate role, CPython and qualifiedR10 reference.
Retain B346 unchanged. No new framework/profile, exact-allocation claim, publication
or automatic acceptance by a fixed percentage. If regressions survive, address the
shared call/dispatch design before calling R13 complete; do not silently spend debt.
