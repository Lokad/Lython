# Call-entry check consolidation — R44

2026-11-07. Baseline source `5fb25354`, core `9eb7a6b5` (precommit metadata
`58d14512`). One deletion: twelve wrappers in LythonRuntime.Calls.cs repeated
CheckExecution immediately after EnterInterpreterFrame, which already performs
that exact check before managed stack/nesting admission. No guest, memory admission
or await intervenes. Delete the duplicate calls; preserve checked entry, exception
translation and balanced finally cleanup.

Per-instruction, loop, long-operation, resume and post-admission checks remain.
In particular, executable constructor checking follows PyInstance allocation and
must stay. No replacement counter, quota, policy, abstraction, switch, API or
checkpoint-counting tests. Broader instruction-check hoisting is not selected:
its cancellation/refusal/error-order proof is incomplete. R42's prefix samples
do not establish a removable guard CPU percentage.

Existing focused Release checks pass **103 internal / 862 public on actual .NET 11**:
call binding/order, native helpers, real callbacks, cancellation, first refusal,
unchanged targets, awaited cleanup, retained aliases and shared-budget reuse.
No new tests duplicate the implementation. Actual .NET 10/full platform/Stress/
installed consumers will be batched with the next coherent runtime checkpoint;
the separate terminal-green R43 delivery42365045704 covers source5fb25354 only.

One short reused complete-job diagnostic covers text/CSV/JSON/report against two
baseline roles with ordinary GC/tiering, identical auxiliary images and shared
CoreLib/process GC. All output/file goldens, budgets, isolated images and fixture
cleanup pass. Allocations are essentially flat and every admitted peak matches.
Desktop clocks vary, including adverse report observations; no CPU gain, parity,
embedding latency or repayment of prior debt is claimed. No VM campaign/repeat.

[Source](source.patch), [entry/source proof](before.json), [focused checks](focused.json),
[complete-job observations](summary.json), [sources/images/raw checks](development.tar.xz).
Diagnostic images are not release packages. Publication remains postponed and
the substantial whole-script performance objective remains open.
