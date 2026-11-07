R31 source gate: FunctionBindingPlan rebuilds positional/keyword parameter arrays,
layout names and name-to-slot dictionaries on every function creation. No mutable
default/closure is needed for those tables. Preserve one immutable parameter layout
on lowered definitions/lambdas and share it with executable bindings (including
captured record copies). FunctionBindingPlan retains only mutable default/keyword
name state, callable identity and a reference to that layout. Existing call binder,
operand storage and executable/lowered/generator routes remain the consumers.

Compiled metadata is covered by the existing coarse compilation reservation
(32768+96/source character); guest functions cannot project as callable CLR values,
and code cannot escape beyond a public run. Internal closures/aliases retain the
same code under its existing compiled/execution lifetime. Projection is independent.
Do not introduce a new lease/cache or memory subsystem. Native generated layouts
use the same construction algorithm with the original conservative capacity
preflight and rollback through successful plan construction. Shared prepared
metadata is never refunded on a later default-construction refusal. Keep per-
function keyword PyStrings and mutable defaults/attributes private; current public
identity and storage ownership are not silently shared. This narrows the original
keyword-name proposal: no claim that all per-definition construction disappears.

Executable local-name maps contain body locals and cannot replace the complete
parameter-only binder map for lowered/generator/dynamic consumers without another
representation change. Do not add a parallel lookup route just to reuse that map.
The single parameter classification/layout algorithm moves to prepared metadata;
its per-function runtime copy disappears. No claim of per-call dispatch CPU savings.

Acceptance: focused function/default/keyword/lambda/generator/class/typing,
cooperative cancellation and ownership/refusal cases on actual11/10. Add externally
observable repeated-definition/default mutation coverage only where existing tests
lack it. Precise compile/warm class-factory and representative controls; keep prior
adverse conditions. Quiet whole-script comparison and full platform/original Stress/
installed consumers follow at the coherent retained delivery boundary. No speedup
forecast, new framework, scope audit or fixed percentage veto.
