# Ordered prepared native formatted bodies

2026-11-05. Retain refined source, frozen core **d8d8df23**, parent0997baa8 plus
exact patch/snapshot. Baseline **0997baa8/68a9f3f8** is fully qualified; this change
passes focused actual .NET11/10 checks. Full platform/Stress/installed qualification
is batched at the next coherent delivery. Publication postponed; broader work open.

Compilation proves one root builder, no nested builder, one final stack value and
ordered operations. Consume that order directly. Delete runtime
`NativeReturnShape.Builder`, builder-only proof cases and recursive append-spine
execution. Keep pure field-type checks, actual original reads, shared formatter/
builder, original spans/exception context, cancellation and ownership. No value
cache, array/plan, owner, framework or public option added. Final source is **one
net production line larger**; removed mechanisms, not line count, are the
simplification. Generic native expressions still use their real evaluator.

## Original source and bounded refinement

Initial core **6f73c0c8**, focused checks and all measurement gates pass. Its
formatted-body loop enlarged GuardNativeBody, also reached by non-format helpers.

| Initial job | Elapsed vs baseline / unchanged control | Baseline spread | Candidate IQR |
| --- | ---: | ---: | ---: |
| Report warm |−3.19% /−3.50%|0.32%|1.00%|
| Report async/completed I/O |−4.44% /−5.03%|0.62%|2.06%|
| Report compile-to-result |−3.15% /−6.08%|3.11%|4.10%|
| Report compilation |−1.90% /−1.35%|0.56%|0.42%|
| Text warm control |**+2.86% /+1.42%**|1.42%|0.47%|

Allocation flat except compilation +0.03%. Preserve the text cost as a possible
regression. One bounded refinement restores the original tiny shared entry
wrapper: generic proof's FinishFormat case delegates to a formatted-body proof.
Original conditions remain: [plan](initial/plan.md),
[metrics](initial/assessment.json), [raw/source/gates](initial/observations.tgz).

| Refined job | Elapsed vs baseline / unchanged control | Baseline spread | Candidate IQR |
| --- | ---: | ---: | ---: |
| Report warm |−3.46% /−5.43%|2.08%|1.80%|
| Report async/completed I/O |−7.17% /−5.64%|1.62%|1.65%|
| Report compile-to-result |−1.66% /−5.38%|3.93%|1.54%|
| Report compilation |**+2.21% /+1.75%**|0.46%|0.86%|
| Text warm control |**+0.85% /+0.92%**|0.07%|1.09%|

Allocation still flat except compilation +0.03%. Retain the deleted recursive
builder machinery and favorable complete reports. Accept possible **~1% text
cost** and **~2%/~1µs compilation cost**; fresh-report magnitude remains uncertain.
No measured inlining/CPU attribution or claim the refinement caused the text
difference. Original favorable compile/adverse text results stay. No automatic
next variant/retiming or permanent veto. Percentages are separate source pairs,
not additive CPython ratios. [Refinement plan](refined/plan.md),
[metrics](refined/assessment.json), [raw/source/gates](refined/observations.tgz).

## Conditions, validation and staging

Both **135 s** five-job collections use the existing complete-job driver, quiet
Azure F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering, three isolated images,
two unchanged roles and identical auxiliaries/CoreLib/string hashes. Six seconds
guest warmup/image, two seconds settle, nine rotated ~200ms batches; 180s owned
ceiling. Full independent CPython3.13.16 stdout/files, exact binaries, normal
runtime/quiet/budget baselines/finalzero, closes/joins/fixture removal pass.
Invocation/mediated RAM I/O/full checks/result cleanup included; reset/staging/
startup/hash/final cache teardown excluded. Fresh adds Compile/Dispose; compile
excludes execution/I/O. Process allocation includes reset/background work.
No CPU/RSS/startup/actual-pending-I/O or updated external-ratio claim.

Each source: **275 internal/1,167 public tests per actual .NET11/.NET10**, including
format/native-return/cancellation/refusal/retained-result/generator/real pending
host controls. Nonincremental Release builds50.97s/33.90s, zero warnings/errors.
No policy-only tests, checkpoint counters, timeout relaxation or stress reduction.
[Validation](validation.json), [logs](validation-logs.tgz).

Refined setup's unchanged64MiB free-disk guard stopped staging before a service/job
started. Verify local hashes, then remove nine redundant uploaded payload archives
only; frozen workers/sources/evidence untouched. Free space60,215,296→72,040,448 B.
Same guard/source/identities/parameters then pass. Raw refinement archive includes
receipt/hashes. This is staging correction, not performance evidence or failed
timing; both conditions are preserved.
