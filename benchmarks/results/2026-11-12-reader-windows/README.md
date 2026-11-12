# Immutable reader windows — retained with explicit latency tradeoff

2026-11-12, R271 over qualified23997d4f. Non-inline line/read values borrow an
immutable admitted window. Their offset uses the existing inline word/tag, with
no ordinary-wrapper size or hash-policy change. The next raw refill admits a
replacement and copies only carry; close allocates nothing. Full backing funding
moves to the existing execution/result owner before allocating the view. Its
conservative command lifetime can deny earlier. Views refund only their shell.
Per-line payload arrays/copies disappear; inline and accumulated values stay owned.

5,506 final focused case-executions pass across actual.NET11/10, including storage,
Unicode/codecs/CRLF, retained aliases, original regex subjects, pending cleanup,
cancellation/refusal before host read/cursor mutation, shared allowance and reuse.
Counts include overlapping filters, not unique tests. Original failures remain:
private-reader ownership can transfer to the command (assert total funding), and
sync execution requires an immediate host. Neither correction relaxes safety.

Six complete search/guarded-search/CSV/captured-text/report/JSON scripts pass
Run/RunAsync, separate compilation and compile-plus-one-run, full stdout/files,
balances and cleanup. Desktop warm allocation falls17.93–17.97% search,5.48–5.51%
captured text and5.10–5.11% JSON; CSV/report effectively flat. Compile allocation
is flat; fresh reductions are smaller. Desktop elapsed controls move substantially,
including adverse report observations; no desktop CPU claim. Desktop source was
not contemporaneously frozen: comments/using order and four tests changed later.
Original desktop bits remain, with that limitation. Exact current source snapshot
and matching build were frozen for the VM; all568 production hashes still match.

## B622 controlled quiet comparison

Fixed fec79 comparison worker, normalGC/tiering, optional adapter0.4; F4as_v7,
Ubuntu24.04.4/.NET11RC3. Qualified23997/d51f versus frozen prototype. Four jobs,
four processes/job (two/image),5swarmup (12s captured text),7rotated250ms rounds.
156.771seconds. All source/image/output/allowance/quiet/join/unmount/archive gates
pass; original RAM fixtures removed only after joined users and verified archive.

| Complete warm Run | Elapsed change | Wider process CPU change |
| --- | --- | --- |
| Native search | −6.26..−1.95% | −7.05..−0.77% |
| Captured text | −5.39..−0.06% | −4.57..+0.60% |
| JSON-lines helpers | +0.01..+1.50% | 0.00..+2.86% |
| Report control | −1.13..+2.55% | 0.00..+7.79% |

Ranges span all process-median pairings, not confidence intervals. Report reference
spread3.49% and250ms batches/10msCPU ticks leave global-access cost unresolved.
Warm clocks include mediated RAM I/O, full validation and prior-result cleanup;
exclude compilation/startup/reset/protocol/finalcache. Wider CPU includes reset,
protocol and processGC/JIT. No CPython update or attribution from allocation alone.
A bounded two-job JSON/report confirmation with longer batches follows below.
No full delivery replay was started before the retention decision.

Space recovery retires only exact terminal canonical-Git/SHA duplicates and the
superseded a552 image (all11aliases), after a fresh lease/UID/path/inode/link proof.
Qualified d51f/current images remain. OS240MiB/evidence12MiB floors stay unchanged.
One targeted inode-alias inventory was necessary; no broad usage audit. Original
staging inventories and all retired bytes survive in committed canonical archives.
B622 terminal state is cached. Publication remains held.

## B623 confirmation and retention decision

Identical binaries/catalogs/normal runtime profile; two jobs, two processes/image,
8swarmup,7rotated750msrounds.121.705seconds. All identities/goldens/balances,
quiet/join/unmount/archive/localSHA checks pass. Terminal status is cached.

| Complete warm Run | Elapsed change | Wider process CPU change |
| --- | --- | --- |
| JSON-lines helpers | −2.62..+1.72% | −2.69..+1.85% |
| Report control | +0.62..+2.77% | −0.04..+2.03% |

Report controls1.13%/0.98%, maxIQR1.96%. A small elapsed regression survives the
longer batches; the earlier report CPU+7.79% bound is not reproduced. JSON reference
spread3.90% leaves its direction uncertain. Original B622/desktop adversity remains.
Retain the immutable storage/copy removal for the established18% search and5%
text/JSON allocation savings and modest search CPU benefit. Accept report's small
elapsed debt explicitly; no further unchanged retiming, equivalence claim, broad
CPU gain or extrapolated CPython ratio. A later end-to-end representation redesign
may address hot access; useful source changes/new evidence can justify revisiting.
The substantial whole-script performance objective remains open.

The first confirmation launch had a Python quoting syntax error, before any remote
mutation or worker. Original script/error survive; corrected script is parsed before
launch. Exact terminal B622 duplicates were retired only after committed canonical
archive verification/fresh lease/UID/path/inode proof, keeping both image families
and existing floors. Publication remains postponed. Platform/stress/installed
Release package qualification follows the retained-source commit.

First full delivery found an old close-refund fixture assumption; its useful
private-reclamation check is preserved alongside stronger shared-capacity checks.
[Original failure and correction](close-fixture-correction/README.md). No production
change or retiming. [Service metadata clarification](measurement-metadata-correction.json)
preserves the inherited live-unit labels and authoritative actual launch/terminal
identities. Full corrected-source delivery remains required.

Corrected-source full delivery is now [GREEN](delivery/README.md) at beeb828e,
with all568 measured production sources proven equal. No unchanged replay.
