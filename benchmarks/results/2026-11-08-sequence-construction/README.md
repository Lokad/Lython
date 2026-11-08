# Intrinsic sequence construction — R72/B428

2026-11-08. Retained change on a34f56b5; measured cores a0aef9ef (.NET 11) /
a31eaa94 (.NET 10), exact source inventories/patch/binaries preserved. Full combined R71 correction + R72 delivery is GREEN, CI42428096630:
Windows/Ubuntu Release actual11/10, original Stress and installed core consumers.
[Cached terminal receipt](delivery-ci.json); never query again. Publication postponed.

## Removed phase and focused acceptance

The shared instruction prefix now executes the whole plain MakeList/MakeTuple
family using its existing admitted builders. Their two ExecuteStructure cases
and unconditional lexical-context promotion disappear. No new constructor, return
shape, cache, execution engine or ownership policy. Starred displays, set/dict key
protocols, match and real lexical-context consumers retain existing behavior.
Tuple backing allocation gains the post-admission cancellation check already used
by list construction. Overall production change deletes two net lines.

Evaluated inputs retain order, exact identities/aliases, capacity and scalar
headroom, replacement overlap, rollback and retirement/publication semantics.
No API or intended Python behavior change; fewer scratch/context admissions can
avoid prior conservative rejection, not promise identical tiny-budget thresholds.

**1,729 internal / 597 public checks per actual .NET 11/10 pass**, matching probes.
Before runtime edits, all twenty deferred-context tests pass on corrected R71:
ordinary/keyword calls, sequence identities, errors/exception context, real pending
I/O/cancellation, withdrawn aliases, escaping results and budget reuse. Four new
refusal cases admit the work area first, then reject substantial literal storage;
they preserve line-five failure origin, stop publication/guest catch/finally return
and reuse the same budget successfully afterward. Existing function/generator/
recursion/stack/collection/native protocol/projection/refusal/cancellation/lifetime
checks remain. No architecture-only assertion or relaxed timeout/containment check.

## Allocation and diagnostic desktop clocks

Existing five-script driver, both APIs, normal GC/tiering; independent old/current/
old-control assemblies sharing CoreLib/GC/hashing. Goldens, budget balances, image
isolation and fixture cleanup pass. Returned-record CSV allocation falls a further
**23.48% sync / 21.69% async** against corrected R71. Ordinary CSV/text/JSON/report
allocations essentially unchanged. These percentages are separate source pairs,
not additive to earlier R70/R71 improvements.

Desktop elapsed is diagnostic: affected sync −7.12–8.94%, async −1.66–2.78%;
adverse ordinary CSV async +3.26–4.89% and report sync +1.31–2.38% remain. Text
old-role drift −15.81% sync/+7.81% async and contradictory other observations are
preserved, not promoted as CPU evidence.

## Controlled complete-script source pair

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, unchanged 7cf37ca5 worker and normal optional
0.4 adapter. Baseline bc30d424 matches corrected R71/a34f56b5: focused checks and
B427 semantic/ownership/golden gates passed; its full corrected delivery batches
with this source. Earlier f88191f7/R70 green checks do not qualify this source.
Two baseline/two current processes, five-second warmup per role/job, seven rotated
150 ms rounds, normal GC/tiering. Four jobs complete in **114.025 seconds**.
Negative elapsed means faster.

| Complete job | Elapsed change, all process pairs | Baseline / current spread |
| --- | ---: | ---: |
| csv-validated-records.large / warm | -4.79 to -1.92% | 0.37% / 2.57% |
| csv-validated-records.large / warm-async | -6.49 to -2.76% | 1.35% / 2.53% |
| csv-summary-functions.large / warm | -0.27 to +0.86% | 0.82% / 0.30% |
| text-summary-helpers.large / warm | -1.53 to +1.61% | 0.73% / 2.38% |

Affected elapsed improves across both candidate processes. Ordinary CSV stays near
baseline; text varies with a 2.38% candidate spread. Coarse process CPU is favorable
for the affected script, mixed for ordinary CSV and adverse up to **+6.67% for text**.
Its 10 ms quantization and outside-job work limit precision; retain the adverse
result without a causal explanation or general CPU-gain claim. Keep the complete
phase deletion, allocation reduction and controlled affected elapsed benefit.
Remaining ordinary text/regression debt is considered at the combined checkpoint;
this does not complete the whole-script performance objective.

Warm includes public invocation, mediated RAM I/O, full stdout/file validation and
prior-result cleanup. Compilation/startup/staging/reset/protocol/final cached teardown
excluded. Async timing uses completed I/O; actual suspension is correctness-tested.
Desktop includes reset/result disposal and absolute clocks cannot be compared.
No CPython ratio, RSS/startup/pending-I/O/embedding-tail claim.

All source/actualReady/auxiliary/runtime/golden/quiet/Closed/exit/join/empty-fixture
gates pass. Persistent records remain separate from owned 128 MiB RAM fixtures;
all users join/mounts disappear before archive verification under lease and locally.
Staging 271,699,968 B passes the 256 MiB floor after reclaiming only the redundant
B426 profiles extract: its immutable archive SHA6071077a was verified first and
both archive and independently verified local evidence remain. Exact path/free-byte
receipt retained. 150 s owner + 30 s stop + 30 s margin; no measured timeout/restart.
B428 is terminal/archived/verified, never query again. Publication postponed.

[Controlled assessment](assessment.json), [allocation/adverse desktop](local-assessment.json),
[focused checks](binding-focused.json), [identities](identity.json), [VM identities](vm-identity.json),
[pre-code gate](pre-code-plan.md), [contract](contract.md),
[reclaimed redundant extract](reclaimed-redundant-extract.json),
[VM archive](observations.tgz), [local raw/source/cores/tests](local-observations.tar.xz).
