# Deferred repr cycle storage, 2026-10-18 UTC

Scalar repr rendering no longer constructs an unused container-cycle HashSet.
The existing tuple, sequence/view, dictionary and nonempty-set entries create it
when needed and pass the same reference-identity set through the same recursive
dispatch. All Add/Remove, finally cleanup, frame/stack checks, string rendering,
protocol calls and output preflights remain. Empty-set rendering needs no set.
No new dispatcher, type classifier, cache or ownership mechanism is added.

Baseline: `b0ac7c603e4a235d9aab0bab5b37cb46fa5aa436`; candidate: that revision
plus the [one-file patch](candidate.patch). One frozen warm native allocation pair
uses prepared code/context/input, two warmups, eight samples and eight calls per
sample. Compilation, startup scheduling, public projection and host I/O are
excluded. Pure async executable operations complete on the measured thread.
Direct-native rows call synchronous rendering under either harness mode.

| Rendering shape | Before | After | Saved per call |
| --- | ---: | ---: | ---: |
| Direct native text | 360 B | 296 B | 64 B |
| Direct native integer | 208 B | 144 B | 64 B |
| Executable repr(text), either API | 704 B | 640 B | 64 B |
| Executable repr(integer), either API | 552 B | 488 B | 64 B |
| Executable bool/None/empty-set repr, either API | 408 B | 344 B | 64 B |
| Executable repr(float), either API | 584 B | 520 B | 64 B |
| Executable ascii(text), either API | 1,064 B | 1,000 B | 64 B |
| Executable !r field, either API | 672 B | 608 B | 64 B |
| Seven nonempty/cyclic container shapes, both APIs | Unchanged | Unchanged | 0 B |

All 14 container allocation arrays, committed funding, work and output hashes
match. All 304 fuel/error/span records and 64 shared-admission records match.
The removed small control object used existing runtime headroom; guest storage
charges and admission policy do not change. Output remains funded, temporary
reservations unwind and owner disposal returns the shared allowance to zero.
There is one capture per revision, with no recapture or production tuning.

All 327 distinct focused Release checks pass: 43 white-box, 132 public rendering/
protocol cases, 90 admission/ownership cases and 62 complete-job/cancellation
cases (58 complete CSV/text/JSON/file jobs). Existing coverage includes Unicode,
cyclic containers, protocol I/O/cancellation, retained results/caches and terminal
refusal. No permanent architecture-specific test is added; the temporary
diagnostic is removed. Original Stress scales, assertions and timeouts remain.
Six independent programs match CPython 3.13.2 through both APIs before/after,
including aliases, cyclic list/dict/tuple/views and scalar/empty values.

Local Windows 10.0.26200 X64, i7-14700KF/28 logical processors, 68.4 GB;
SDK 10.0.300-preview.0.26177.108, runtime 10.0.13. Allocation only; elapsed time
was not measured. No CPython, compile-to-result or complete-job speedup claim.
No public contract changes require a SPEC/MEMORY revision. Package/platform
qualification is recorded in the private plan. Regex remains planning-only.

[Scope](measurement-scope.json), [source screen](source-screen.json),
[diagnostic](diagnostic.cs), [baseline](baseline.trx), [candidate](candidate.trx),
[comparison](comparison.json), [baseline probes](baseline-probes.json),
[candidate/CPython probes](candidate-probes.json) and [validation](validation.json)
preserve the evidence. Stop at these existing repr consumers.
