# Await absolute-value guest protocols, 2026-10-18 UTC

`RunAsync` now awaits guest `__abs__` callbacks for both `abs` and `operator.abs`.
Previously, `abs` selected its synchronous implementation: a callback opening
a file through an async-only host failed with “stat cannot run synchronously”.
The shared absolute-value consumer in `operator.abs` also had only a synchronous
registration and now uses the same async value helper.

The correction uses existing unary-protocol lookup/invocation. Primitive values
complete without a new async state machine; guest instances use the awaited
protocol route. Missing hooks fall through to the existing numeric/type-error
kernel without a second lookup. No binder, cache, execution mode, ownership or
reclamation mechanism is added. The redundant synchronous `EvaluateAbsolute`
forwarder disappears. Numeric operations and allocation preflights are unchanged.

## Regression and validation

Baseline is immutable **ff4b52f0a326346a8ad0d5eea0813f5265b02110**. The
[frozen four-file patch](candidate.patch) corrects registration and dispatch,
extracting the existing numeric kernel without changing its statements.
[Validation](validation.json) checks that source equivalence and records hashes.

The [baseline protocol run](baseline-protocol.trx) passes 14 existing controls
and fails the two new global-abs cases for success/cancellation. After the fix,
all 18 controls pass, including two new `operator.abs` cases. The sibling
operator's synchronous registration is source-confirmed; it was not separately
executed on the baseline. Both callbacks now suspend during host I/O, preserve
effects exactly once, cancel correctly and return shared allowance to baseline.

All **965 distinct final Release cases** pass: 50 abs/scalar cases, 99 white-box
protocol/stack/architecture cases, 665 public operator/binding/numeric controls,
89 ownership/admission and 62 complete-job/cancellation cases. The last group
includes 58 complete CSV/text/JSON/file jobs. Original Stress coverage is retained
separately; no fixture, containment assertion or timeout is reduced.

New pressure cases run both functions through both APIs. A guest hook performs
an earlier file effect, then attempts a denied allocation and returns from
`finally`; its caller also attempts to catch MemoryError. The command still
fails with its execution-memory attribution, produces no guest output and keeps
the earlier effect. Disposal returns the shared allowance to the compiled
baseline, then to zero when compilation ownership ends.

The 26 numeric programs independently match isolated **CPython 3.13.2** with
`-I -S -X utf8`; [goldens](python-goldens.json) retain the semantic evidence.
They include signed zero, numeric types, arbitrary protocol results, missing/
noncallable hooks, arity, aliases and expanded calls. [After-fix receipts](async-fix-protocol.trx)
and process records preserve the actual suspension regressions.

## Limits and next work

No allocation/timing, complete-job speedup or CPython performance comparison was
run or claimed. Existing primitive argument packaging remains. A possible native
single-input call path is separate planned work, requiring frozen work/spans and
one short allocation/control comparison before dispatch changes. JSON member
wrappers already use the module cache, and argument presence is already inline;
neither source screen justified another mechanism.

Host mediation, managed stack admission, terminal refusal, cancellation and
funding until awaited cleanup remain unchanged. Runtime dependencies and the
deferred regex migration are unchanged.
