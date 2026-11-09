# Native-return consolidation source gate (R168)

Assessed after R167/43350e4f,2026-11-09; exact unchanged source hashes in
[assessment](assessment.json). No runtime prototype, retiming, new framework or
repeated unchanged semantic qualification. This is a bounded design decision,
not a fresh CPU attribution or permanent rejection of shared execution.

Hypothesis: retire the closed native-return tree by executing ordinary instructions
while preserving its borrowed-input/no-workspace behavior and measured allocation
benefit. Source establishes a concrete tradeoff:

- Native call entry borrows the caller's evaluated operand span before binding.
  Its closed proof precedes admission/effects. Actual work never retries/falls back.
- Ordinary executable calls already bind into final local/operand storage and can
  defer the lexical invocation context. Their work state still requires array-backed
  value storage, supplied by the existing admitted/reused function workspace.
- Native return nodes avoid that binding/workspace path and recursively evaluate
  the closed expression. Primitive operations and scalar shape facts are already
  shared; this is not a duplicate set of Python operator implementations.

Blindly routing native helpers back through ordinary entry would restore work the
optimization removed. Preserving both one instruction engine and this input/storage
behavior requires a broader operand-storage design; it is not a bounded dispatcher
deletion. Do not introduce a second accessor/dispatch framework, speculative caller
windows or an arena merely to describe the same storage differently.

The previous [call arena](../2026-11-01-call-arena-rejected/README.md) actually
removed allocations, yet complete CSV/text regressed5.0–6.8%; its JSON control was
inconclusive. [Call-control records](../2026-11-01-call-control-rejected/README.md)
retained allocations and regressed. Old frozen percentage/no-retiming rules are
historical, not current policy; those results are evidence about added work rather
than a permanent veto on a new justified design.

Retain the measured native-return optimization and its correctness fixes. A future
consolidation must identify storage/work eliminated, remove the displaced native
representation, preserve mutable globals/proof-before-effects, actual async fallback,
source diagnostics, cancellation/refusal/alias funding and managed stack safety,
then compare complete CSV/text with unchanged controls. No new representation is
selected from the current capture's inclusive prefix or native-call sample shares.

Stop this source phase at the explicit storage/guard constraint. R167 is the completed
construction simplification; next is a representative compile/warm/compile-to-result
CPython refresh at the coherent delivery checkpoint. Publication remains postponed.
