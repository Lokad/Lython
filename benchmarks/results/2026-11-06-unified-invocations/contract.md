# F03: one invocation context and executable state, before implementation

Baseline c599dac6; F02 has focused qualification only, K01/dd76df36 has cached
terminal42145148043 GREEN/all8. Preserve completed qualification; qualify the
combined changed runtime at a coherent retention boundary. Publication postponed.

Hypothesis: ordinary executable calls can allocate one actual invocation context
instead of a context plus a separate interpreter pointing back to it. Put the
existing executable state/loop on that context; retain one instruction engine.
This removes the separate interpreter allocation/backpointer, stored active-frame
pointer and generator's duplicate context/interpreter handle. It does not move
callers to saved records, use an arena/pool, unbox values, or add a dispatcher.
F01's distributed samples do not predict a speedup. B61/B69 slower variants did
not remove this actual pair; preserve their results and avoid relabeling them.

ExecutionContext inherits the existing local/cell-view state, which retains the
actual namespace parent via ExecutionFrame. The interpreter partials become
ExecutionContext partials; all operations use the same invocation object directly.
Root/import/function/generator production entries already have distinct contexts.
Generator reentry remains rejected; imports use pre-published module namespaces.
Raw internal execution must reject overlapping initialization before any state or
admission mutation, never silently select another backend. Sequential reuse stays.

Working slots/caches/handler records are initialized for execution and detached
only after actual users/awaited cleanup finish. Captured lexical contexts keep
their names/cells/namespace but no retired operand array or borrowed workspace.
Suspended generators retain state until completion; a single retained handle
owns it. Ordinary child completion reads return/error before detaching and then
resumes its caller's unchanged live state. Internal frame views are invocation
borrows, not post-completion snapshots; preserve external values/side effects in
tests rather than pinning the discarded internal layout.

Tradeoff: plain class/annotation/comprehension and native-return-only contexts
gain inline state fields; verify the existing conservative closure headroom and
measure a method/native-return control. Document conservative admission changes
if needed. No hidden budget, tracing, forcedGC, retry, native stack or new policy.

Acceptance: defaults/keywords/locals/global/nonlocal/class-cell behavior, recursive
and callback calls, source/exception state, generators/send/throw/close, imported
modules, real suspension, cancellation, first refused admission, funded aliases,
retained closures/results and deterministic cleanup. Focus existing regressions;
add a real lifetime/reuse witness where the merged identity creates a gap. Remove
architecture-only scaffolding rather than keep a compatibility interpreter.

Completion: separate interpreter type/backpointer and generator duplicate field
gone, no refunded workspace reachable from completed lexical contexts, same engine
and managed admission. Then bounded before/after using existing complete-job
drivers: CSV warm/fresh/async, text/report controls and simple-method control.
NormalGC/tiering, quiet VM/lease, two unchanged controls, frozen bits/sources,
full goldens, closed/final funding and all users joined. Separate allocation
from elapsed; retain all adverse/unknown results. Initial <=210s combined block;
bounded refinement requires specific source/measurement evidence. Decide any
remaining regression debt at this coherent milestone, not via fixed thresholds.
