# One binary operator kind, 2026-10-18 UTC

Executable instructions now use the existing internal BinaryOperatorSyntax.
This deletes a duplicate 25-member enum, the syntax-to-executable and reverse
maps, and a trivial runtime forwarder. Sync, async and simple-return consumers
call the existing operator routines directly. Seven files change: 105 removed
lines and 13 added lines. Two existing test enum references change; their
assertions remain. Unary/augmented operators and instruction layout stay as-is.

Baseline: `38f44447e5bc44faeae3cfe5be678f28305be266`; candidate: that revision
plus the [seven-file patch](candidate.patch). This is internal consolidation,
with no public API or persisted bytecode change. `and`/`or` already branch to
short-circuit lowering before translation and never emit eager binary opcodes.
No new validation table, dispatcher, cache, owner or configuration is introduced.

One frozen allocation-only pair covers native compilation and warm execution
of binary/mixed-number loops, chained comparisons and helper functions, with
unchanged augmented/unary controls. All 18 eight-sample allocation arrays,
work counts and opcode/operator-name/source-span fingerprints are identical.
All 96 result/error/fuel/span records match. Seven existing standalone operator
goldens independently match CPython 3.13.2. No elapsed timing was taken and no
latency, allocation reduction, CPython or complete-job gain is claimed.

Native compilation includes frontend/lowering/executable code construction;
it excludes the public compile admission/owner wrapper. Warm execution excludes
compilation, context setup, startup workers, public projection and host I/O.
CPU-only async values must finish on the measured thread. Two warmups, eight
samples and eight calls/sample; one capture per revision, no retiming/tuning.
Machine: Windows 10.0.26200 X64, i7-14700KF/28 logical processors, 68.4 GB;
SDK 10.0.300-preview.0.26177.108, runtime 10.0.13. No quiet-machine timing claim.
An initial diagnostic compilation failed nullable ValueTask branches; splitting
those branches fixed the fixture before the sole successful baseline capture.
No production change is claimed for that fixture correction.

All 572 distinct focused Release cases pass: 111 white-box, 309 public operator/
protocol/short-circuit cases, 90 shared-budget/owner/refusal cases and 62 complete-
job/cancellation cases (58 CSV/text/JSON/file jobs). They exercise actual suspended
numeric/rich/containment callbacks, cancellation, wide integers, admission and
retained aliases. Allocation preflights, arithmetic kernels, managed stack and
ownership machinery are unchanged. Original Stress scales/assertions/timeouts
remain. The temporary diagnostic is removed; no architecture-only tests added.

[Source screen](source-screen.json), [frozen scope](measurement-scope.json),
[diagnostic](diagnostic.cs), [baseline](baseline.trx), [candidate](candidate.trx),
[comparison](comparison.json), [CPython goldens](goldens.json) and
[validation](validation.json) preserve the evidence. Native captured frames,
temporary reservations and the allowance return to baseline/zero. Package and
cross-platform delivery receipts are recorded separately in PLAN.md.
Stop at binary-kind consolidation. Regex remains planning only.
