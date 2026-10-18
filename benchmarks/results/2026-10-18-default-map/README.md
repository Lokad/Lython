# General function-default map capacity, 2026-10-18 UTC

The two existing general default-map builders count non-null defaults and allocate
their ordinal dictionaries with that capacity. Intermediate dictionary growth
disappears for larger signatures. Evaluation order, callbacks, RuntimeValue,
default aliases, caller preflights and scratch cleanup remain unchanged.
No shared defaults, cache, owner, new helper or memory-contract change.

Baseline: `10d108eb5d04b10d2944ff838cc203f7ee2ae179`; candidate: that revision plus the
[four-line runtime patch](candidate.patch). The [scope and gate](scope.json)
precede runtime edits. The exact measured source is final; no further tuning.

| Native helper / defaults | Baseline | Candidate | Status |
| --- | ---: | ---: | --- |
| General sync and async / 0 | 112 B | 112 B | Identical samples |
| General sync and async / 1 | 248 B | 248 B | Identical samples |
| General sync and async / 8 | 1,024 B | 472 B | 552 B less per map |
| General sync and async / 32 | 2,112 B | 1,200 B | 912 B less per map |
| Existing executable control / 0 | 80 B | 80 B | Identical samples |
| Existing executable control / 8 | 496 B | 496 B | Identical samples |

One corrected baseline and one candidate use two warmups, eight samples and 64
maps per sample for each route. All arrays are stable. Required parameters are
interspersed with defaults; final key order, exact evaluation counts and live
supplied-list aliases match. [Diagnostic](diagnostic.cs), [baseline TRX](baseline-native.trx),
[candidate TRX](candidate-native.trx), process receipts and [comparison](comparison.json)
preserve raw evidence. The temporary diagnostic is removed.

Local Windows x64 build 26200, Intel Core i7-14700KF (28 logical processors),
.NET SDK 10.0.300-preview.0.26177.108/runtime 10.0.13. [Environment](environment.json)
reuses earlier configuration with its original timestamp. This measures warmed
native map-construction allocation, excluding guest work, compilation, projection,
scheduling and startup. No elapsed, compile-to-result, whole-job or CPython gain
is established; an extra counting pass has not been timed.

All 306 distinct focused Release checks pass, including 58 complete CSV/text/
JSON/file jobs. Existing tests cover 40-parameter retained defaults, mutable
metadata/aliases, methods/lambdas/generators, actual default/callback suspension,
cancellation, refusal/rollback, retained results/caches, managed stack and shared
budget cleanup. Eight [existing CPython programs](../2026-10-18-binding-plan/goldens.json)
and their permanent tests are reused. No new architecture-only tests.
[Validation](validation.json) records filters, receipts and counts.

Initial diagnostic compile/span/xUnit mistakes and six missing-default fixture
failures were corrected before production edits; named arguments put expressions
in DefaultValue instead of Annotation. Assertions and the gate were unchanged.
The incomplete initial capture is rejected, with [receipts](fixture-corrections.json)
preserved. These were diagnostic defects, not runtime findings. Original Stress
scales/assertions/timeouts remain. Release/platform qualification is recorded
in PLAN; regex remains planning only.
