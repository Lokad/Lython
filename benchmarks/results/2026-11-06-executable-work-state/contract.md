# R13 context-on-demand / staged source contract

Baseline production88f12099 (fully qualified), HEAD1b9ae3cf adds receipts/evidence.
R12/B345: two complete CSV/text jobs,1.886s capture, all gates; current CSV still
constructs424-byte lexical/executable contexts from BeginPendingCall. Arguments
already bind final cached locals; there is no second copy/namespace to remove.
Allocation ticks are approximate triggering-type attribution, not CPU fractions.
Text's retained native-return path does not have this same context cost.

Hypothesis: ordinary closed helper execution can use the same executable work
state/instruction handlers before a lexical identity is required. Actual scope,
protocol, nested guest or async work materializes a normal context and adopts the
current instruction/locals/storage. No restart, replay, admission retry, context
swapping, pool, saved caller records, slot arena, second interpreter or owner API.
The old B61/B69 regressions remain evidence against their extra copies/indirection;
shared CSV keys remain excluded by parallel mutation/query/iterator requirements.

A: group code/locals/cells/operand storage/instruction position into one inline
ExecutableWorkState. Collapse ExecutableFrameState into the existing concrete
ExecutionContext, remove its three pass-through constructors and old distributed
field declarations. All normal handlers use the one state directly; no field-name
compatibility properties or duplicated values. No extra heap allocation. Preserve
Frame/current-context identity and direct argument/local/operand backing. Adapt
white-box inspections to the actual state without weakening live-input, tail,
retention, refusal, cancellation or cleanup assertions. Focused Release both
runtimes; no speedup claim/isolated benchmark/full delivery replay for factoring.
This is an active foundation, not completion of the performance milestone.

B source proof before activating a new path: existing native operation selection
must distinguish actual context needs before invoking a protocol/host/async hook,
using current values after argument evaluation. Share real opcode implementations;
do not build another native statement interpreter or duplicate arithmetic/binding.
Keep native-return/format/UTF8 optimizations in use until a common path proves it
preserves their behavior and measurements. The module/defining lexical context
must stay unchanged (live globals/closures cannot see a temporary callee frame).

Use admitted final bound storage and existing coarse control estimates from the
start. Function/interpreter admission and cancellation remain balanced exactly
once. On promotion, adopt current locals/IP/operands rather than initializing or
rebinding again; receiver/default/parameter mutations must survive. First refusal
is terminal; no fallback after it. Native reads/writes must either stay protocol-
free through actual admission/reentry or promote before the callback without
repeating guest effects. If that boundary cannot share the existing helpers,
reject/refine the source design instead of adding parallel dictionaries or dispatch.

Scope/capture/generator/handler-sensitive bodies may start with a real context;
ordinary closed helpers are the selected target. A real error must retain Python
traceback/name/span and relevant locals; materialization must not need a new
admission after refusal. Calls that actually suspend use the existing linear
runner and awaited cleanup. Bound locals, temporary outputs, aliases and pending
users stay funded until actual use ends. Managed stack/recursion safety remains;
no recursive second execution route or native stack work.

Acceptance: CPython semantic goldens; argument order/defaults/keywords/methods;
late protocol/locals/global lookup and source rebinding after native work; globals
views/closures remain independent; cancellation/first refusal before side effects;
actual I/O promotion, retained operands/results and budget reuse; generator/trace/
exception/finally controls and managed depth. Use existing tests first, adding
independent regressions only for real new promotion boundaries.

Completion B must remove unconditional context construction from eligible helper
execution and leave one shared instruction implementation, with no dormant
prototype path/compatibility scaffolding. If B is withdrawn, remove its unused
foundation unless the base-type deletion independently simplifies real callers.
A alone is not a performance success. Then one bounded same-source-pair CSV
warm/fresh comparison plus unchanged native-return text/JSON/report controls,
normal GC/tiering/quiet VM/identical roles/full outputs and owned cleanup. Separate
compilation/warm/I/O/cleanup boundaries; archive adverse/uncertain results. Refine
boundedly for demonstrated defects; percentages inform rather than veto. Decide
remaining regression debt at completion. Batch full platform/stress/installed
consumers then; refresh representative CPython at a coherent retained checkpoint.
Publication stays postponed; independent regex development continues.
