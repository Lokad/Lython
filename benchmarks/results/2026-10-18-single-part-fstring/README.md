# Single-part executable f-strings, 2026-10-18 UTC

An executable f-string with one formatted part now returns that already observed
and funded string. The final join no longer creates a builder, output buffer and
copy. The join instruction, fuel checkpoint and field/conversion/specifier hooks
remain. Empty, multiple-part and incremental lowered joins keep their behavior.
No compiler, public API, cache or ownership mechanism is added.

Baseline: `c891dc8c3e07da5d241fc999ce5027295685c3cd`; candidate: that revision
plus this [two-line runtime patch](candidate.patch). One frozen allocation pair
uses prepared code/context/input, two warmups, eight samples and eight calls per
sample. Compilation, startup scheduling, public projection and host I/O are
excluded. Pure async operations complete on the measured thread.

| Native call shape, either API | Before | After | Existing-owner charge added before / after |
| --- | ---: | ---: | ---: |
| Text | 576 B | 312 B | 138 / 0 B |
| !s conversion | 576 B | 312 B | 138 / 0 B |
| Custom formatter | 1,296 B | 1,032 B | 138 / 0 B |
| Integer | 704 B | 456 B | 268 / 134 B |
| Dynamic specifier | 1,248 B | 752 B | 526 / 263 B |
| Nested f-string | 840 B | 312 B | 276 / 0 B |
| Empty control | 384 B | 384 B | 0 / 0 B |
| Multiple-part control | 608 B | 608 B | 140 / 140 B |
| Lowered single-part control | 424 B | 424 B | 138 / 138 B |

The six control sample arrays (three shapes, two APIs), work/output hashes,
compiler join counts and all 352 fuel/error/span records match. Dynamic and
nested forms can avoid two joins. No elapsed timing was collected; these are
allocation improvements, not CPython or complete-job speedup claims.

No refund is added. Avoided copies need no new storage charge; original values
keep their owners. Retained results remain funded after another result is
disposed, and pending formatter I/O/cancellation drains before cleanup. A smaller
allowance may now admit a single-part expression that previously failed its
redundant copy. Multiple-part final-join refusal remains covered.

Seven programs match CPython 3.13.2 through both APIs (14 runs), including native
and custom alias reuse, nesting, Unicode conversions, effects/errors and controls.
Immutable baseline passes two and fails four of six new public regression cases;
candidate passes all six. Reuse matches observed CPython optimization; immutable
string identity is not a universal Python guarantee or a promise for all lowered
execution paths.

All 318 distinct focused Release checks pass: 43 white-box, six new cases, 117
existing rendering/protocol cases, 90 admission/ownership cases and 62 complete-
job/cancellation cases (58 complete CSV/text/JSON/file jobs). Original Stress
scales, assertions and timeouts are unchanged. Temporary diagnostic removed.
[MEMORY](../../../MEMORY.md) and [SPEC](../../../SPEC.md) describe the boundary.

Local Windows 10.0.26200 X64, i7-14700KF/28 logical processors, 68.4 GB;
SDK 10.0.300-preview.0.26177.108, runtime 10.0.13. No quiet-machine claim.
[Scope](measurement-scope.json), [source screen](source-screen.json),
[diagnostic](diagnostic.cs), [baseline](baseline.trx), [candidate](candidate.trx),
[comparison](comparison.json), [initial probes](baseline-probes.json),
[candidate/CPython probes](candidate-probes.json), [baseline regressions](baseline-regressions.trx),
[candidate regressions](candidate-regressions.trx) and [validation](validation.json)
preserve the evidence. Exact package/platform delivery is recorded in the private
plan. Stop at this executable join; regex remains planning-only.
