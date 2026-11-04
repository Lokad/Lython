# D15 / B224 pre-code selection

2026-11-04. Base ee8020f0 / core38c03e5e7c88b33eaffef6414063db74ebc5e69677662c9940b440e726dd0d7e.
Baseline frozen worker: ../dictionary-owner-refinement-b223/candidate/.
PLAN.md records selection before code. No timing outcome predicted.

Eliminate runtime positional/keyword declaration arrays, duplicate parameter-name
array/dictionary for executable/generator definitions, repeated partition passes,
and the separate per-function FunctionBindingPlan allocation. The existing plan
becomes base storage for PyFunctionBase, with no replacement dispatch framework.
Compiled code prepares counts/variadic indices once and lends its existing local
names/map. All lookups exclude body-local and variadic names as before. Runtime
synthetic/lowered functions retain one admitted compact layout, not a second backend.

Defaults and keyword key objects remain isolated per function, with unpublished
rollback preserving supplied values. Retain compiled-owner, execution/result-owner,
pending borrow and closure lifetimes. No public contract/API changes or budget cap.
No unsafe code, fuel, tracing, forced collection, native dependencies or global cache.

Source assessment before freeze: retain a conservative320B combined function/empty
attribute-table estimate instead of128B plus duplicated signature charges. Mutable
defaults/name keys remain separately admitted. Parameterless/lowered functions can
be charged earlier; no promise of identical tiny-budget thresholds. Remove unused
BindingPlan forwarding and duplicate Name storage as part of the same construction.

Focused semantics/real suspension/cancellation/refusal/ownership first, then frozen
representative compile/fresh/warm controlled comparison on the existing quiet VM.
Keep allocation and elapsed claims separate, retain adverse controls, decide any
regression debt explicitly. No requalification of unchanged completed milestones.
