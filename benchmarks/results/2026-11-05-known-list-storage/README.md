# Known and empty list backing

2026-11-05. Retain parent5148eda8 plus exact patch/snapshot, frozen core
**b6046395**, versus **450b9162/d8d8df23** with the same representative corpus.
Focused actual .NET11/10 checks pass; full delivery pending with D31. Publication
postponed; broader performance work remains open.

Initial delivery CI41973141592 at d9245ea9 failed one public retained-subprocess-
arguments test on both platforms; all4,104 internal cases per platform passed.
Later .NET10/package/originalStress gates were skipped. The test expected500
small lists sharing literal strings to exceed400,000 B, depending on the discarded
minimum capacity. Replace that threshold fixture with32 distinct4 KiB retained
arguments: tight allowance refusal before all host calls, roomy success with
returned aliases, and cleanup to zero. All132 focused subprocess/shared-memory
cases pass per actual11/10; production bits remain b6046395. This is test correction,
not a runtime fix or relaxed containment. [Original failed delivery](initial-delivery-ci-status.json)
and [failed log](initial-delivery-ci-failed.log) remain visible; subsequent delivery
qualification is recorded separately.

One construction/growth/clear invariant replaces three old storage rules. Known
capacity is respected, empty lists need no element backing, growth starts at8
then is geometric, and clear/nonpositive repetition relinquishes backing before
refund without allocating replacement storage. Delete the unconditional8slot floor,
special first growth and ReplaceWithEmpty machinery: **13 net production lines
removed**. A unique managed empty sort marker preserves mutation-then-clear
detection, without per-list flags, new owners, tracing or public configuration.

Conservative retained capacity, scalar headroom, checked representation bounds,
old/new replacement admission, first refusal and value/alias funding remain.
Known-small initial charges decrease; growth can request more earlier than the
old9slot promotion. Tight-budget thresholds/effect timing may differ. Clear needs
no fresh allowance, but cannot resume an already refused command. Sort's original
backing stays funded through actual callbacks/cleanup. No new memory allowance.

## Four complete-script jobs

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering. Existing driver,
three isolated images/two unchanged baseline roles, same auxiliaries/CoreLib/hash
witness.6s guest warmup/image,2ssettle,9rotated~200ms batches, **108s owned** within
150s. Invocation/mediated RAM I/O/full output checks/result cleanup included;
reset/staging/startup/hash/final cached teardown excluded, users joined.

All exact binaries, independent CPython3.13.16 full stdout/files, quiet endpoints,
normal runtime, budget baselines/finalzero, closes/joins/fixture removal pass.
Process allocation includes reset/background work. No compilation, fresh, async,
CPU/RSS/startup/pending-I/O or new CPython-ratio claim.

| Complete warm job | Elapsed vs baseline / control | Baseline spread | Candidate IQR | Allocation vs both |
| --- | ---: | ---: | ---: | ---: |
| Native-helper CSV |**+2.63% /+0.74%**|1.88%|0.53%|−3.32% /−3.34%|
| Ordinary-helper CSV |−1.01% /−2.83%|1.87%|0.27%|−2.14% /−2.16%|
| Text/helpers |−2.38% /−2.88%|0.51%|0.58%|−8.61%|
| Report control |−3.86% /−2.44%|1.46%|0.80%|−0.22% /−0.21%|

Initial untimed accounted execution peaks: nativeCSV8,415,421→6,776,621 B,
ordinaryCSV8,415,570→6,776,770 B, text3,432,391→1,957,831 B,
report4,672,623→4,631,663 B. Rough capacity/headroom reductions are **not physical
heap/RSS evidence**. Native CSV's original adverse elapsed result stays visible.
[Initial plan](initial/plan.md), [metrics](initial/assessment.json),
[raw/source/identities/gates](initial/observations.tgz).

## One unchanged-bit native CSV confirmation

Same core/corpus/boundary; reverse initial image construction/warmup,10s guestwarmup
and9rotated250ms batches. **41s owned**,75s ceiling. Every original gate passes.
NativeCSV elapsed **−0.51% /−1.00%**, original spread **0.49%**, candidate IQR0.22%;
allocation **−3.30% /−3.32%**. No production refinement or discarded condition.
The original nativeCSV cost is not reproduced, but remains unexplained and
preserved; no universal nativeCSV gain or CPU cause follows. Retain for deleted
storage machinery, allocation/allowance reductions and favorable text/report
jobs. Ordinary CSV magnitude and native CSV elapsed remain limited by controls.
No automatic further retiming or permanent closure.
[Confirmation plan](confirmation/plan.md), [metrics](confirmation/assessment.json),
[raw](confirmation/observations.tgz). Source-pair percentages are not additive.

## Validation, corrections and storage

Nonincremental Release41.75s/zero warnings/errors. Final **1,561 internal/3,696
public cases per actual .NET11/.NET10** pass: list/CSV/string/JSON, native operations,
admission, ownership, cancellation and real suspended sort protocols. Added full-
allowance clear/*=0 keeps aliases funded and budget reusable without collection;
independent CPython goldens preserve append-then-clear during sorting.

Initial1,560internal passes/two failures required correcting old policy assumptions:
delete the test requiring a replacement allocation/refusal for clear, preserving
clear funding and stronger full-allowance alias coverage; make denied attachment
exhaust the shared parent instead of assuming8slots. Same-capacity mixed-value
funding remains independently checked. No timeout/failure/cleanup assertion or
original stress scale was relaxed. Production bits stay b604 throughout test fixes.
[Validation](validation.json), [logs](validation-logs.tgz).

Confirmation staging initially omitted its empty candidate directory, before any
service/timing. Correct directory and immutable links, keeping exact bits/parameters;
original setup and correction receipt remain in raw. VM space was recovered by
hash-verifying redundant uploads and transferring joined workers' original perf
maps/jit dumps to a verified local archive before deletion. No frozen worker or
used evidence was lost; initial raw includes cleanup receipts. These are staging
corrections, not runtime/performance results.
