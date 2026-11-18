# Configuration compile-to-result cost gate

R508/B932,2026-11-18, qualified0691d64f/frozen65fcf7d0, same exact32file image,
existing181B/16path configuration edit source and previously verified three-case
catalog. Correct the failed warmup's64invocation batch to1024; retain128batch bound,
8guest-second warmup target,5second capture,150sservice cap and254/252MiBguards.
Observed warmup8.073s, captured guest clocks3.907s. NormalGC/tiering plus perf maps.
All goldens/Closed/quiet/join/persistent archive/localSHA/RAM teardown checks pass.
The original B931failed/partial condition remains separate, not relabelled.

All-thread attribution972samples/554unknown leaves, predominantly kernel(481)
and CLR(62); selected guest tids remain in metadata but tables include every thread.
Nearest Lython frames include comparison-host reset220, output reads124, path
resolution82 and file open52. Frontend inclusive287and instruction execution243
overlap useful/inlined work; invocation-setup7/function-entry0 do not demonstrate
a dominant setup/frame allocation cost for this script. All-thread CPU includes
reset/protocol/goldens/I/O/cleanup beyond guest clocks. It cannot be equated to
the published elapsed phase or used as precise removable subsystem percentages.
Group classification now examines executing method owners, excluding signature
return/parameter types; the earlier partial tables retain their original limitation.

No new runtime redesign/gain or CPython ratio is inferred. Compilation is material
to this tiny script, but current separate latency is1.05–1.08×CPython/compile-to-result
~1.08×; presence of passes/allocations alone does not justify another frontend rewrite.
Close this bounded attribution gap and pursue the concrete operand producer stage
identified by current floating IR/partial CPU; broader performance objective active.

Only exact committed old5ae core aliases retired after current65fc's full delivery,
with fresh idle lease/UID/path/hash/link/all-alias proof. Current65fc/auxiliaries/
catalogs/toolchains/unmatched evidence preserved. Failed-worker temporary maps now
archive after join regardless of exit status, preventing the observed failure leak;
original failure path remains in prior evidence. No new benchmark framework or
relaxed timeout/containment assertion.

[All-thread attribution](attribution.json),[raw](observations.tgz),[identity](identity.json),
[archive](archive-receipt.json),[procedures](procedures.tar.xz).
Publication postponed; timing and future source comparisons remain distinct.
