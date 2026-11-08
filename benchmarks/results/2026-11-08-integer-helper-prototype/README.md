# Exact-integer helper intermediate prototype

2026-11-08. Baseline qualified05790317/core13107e6b; prototype over9b61264b/
core d8d2a6fb. Replaces Add-only native proof with conservative exact-integer Binary
proof, reusing original canonical operators. No API/backend/owner/arena/value
representation added. Focused1524public/104internal Release peractual11/10 pass.
Three independent CPython semantic goldens, actual suspended multiply/reflected
protocol cancellation, preflight refusal/after-admission cancellation/cleanup.
Initial assertion/build-identity/post-processing corrections retained in archive.

Two new complete4096-record CSV/JSON-lines accounting variations compute signed
quantity*cents*(100-discount)//100 in a helper, aggregate dictionaries and write a
complete CSV report. Independent isolated CPython stdout/file goldens. These expose
the prior arithmetic proof restriction; they are not unchanged published CPython
cases or an explanation of the historic general performance gap.

B473 existing quiet F4as_v7 VM/frozen worker7cf/optional0.4/normal GC and tiering,
two isolated roles per core, five-second warmup, seven rotated150msrounds.
Six jobs168.664s; all goldens/baselines/Ready/image/quiet/Closed/join/fixture/archive/
local verification gates pass. No users or owned RAM remain. Warm includes invocation,
mediated RAM I/O, full output validation and prior-result cleanup; compilation,
startup/protocol/staging/final cache teardown excluded.

| Script / API | Elapsed change across all pairings | Process CPU |
| --- | ---: | ---: |
| CSV accounting / sync |−5.78% to−1.53%|−0.69% to+2.99%|
| CSV accounting / async |−6.28% to−4.33%|−7.14% to−3.70%|
| JSON accounting / sync |−5.02% to−3.54%|−3.57%|
| JSON accounting / async |−6.67% to−4.57%|−3.85% to−1.54%|
| Unchanged text / sync |−1.77% to+0.20%|−2.41% to0%|
| Unchanged report / sync |−1.61% to+2.98%|0% to+6.25%|

Negative is faster. All ten local warm allocations are neutral. Compile allocation
rises0.40–0.41%affected, controls unchanged; desktop compile clocks uncertain.
The original assessor read the warm-only execution clock for compile jobs; corrected
offline to total compilation/teardown clock, no remeasurement. CSV sync CPU is
uncertain; report has possible regression debt, candidate-role spread3.46%.

**Intermediate, not yet committed or fully qualified.** Source gate revealed that
native proof source offsets disable ordinary operand forwarding even when guards
fail. Complete that coherent design boundary before a final delivery decision:
own immutable source descriptors in existing proof nodes, preserve all operation/
return/member spans, and allow ordinary forwarding independently. Account for the
compiled metadata tradeoff. Reusable ordinary workspaces already avoid repeated
allocation; do not infer an allocation reduction from bypassing that dispatch.

Disk preflight refused before root/mount/workers. Floor/headroom unchanged. Fresh
idle-lease exact-byte hardlinks recovered5.61MB, preserving all paths/aliases/
archives; alias-only inventory failures and the original refusal remain explicit.

[Status](status.json), [controlled roles](assessment.json), [allocation](allocation.json),
[compilation](compilation.json), [identity](identity.json), [receipt](archive-receipt.json),
[all sources, binaries, corrections and raw archive](observations.tar.xz).
