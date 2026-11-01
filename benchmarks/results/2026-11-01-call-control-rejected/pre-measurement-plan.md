### B61 — bounded call-control redesign, before code

Hypothesis: B58's16–18% CSV/13–14% text source effect includes avoidable per-call
control-object construction. Existing direct binding already consumes final
local storage; do not add a binder or combine/replay local/operand buffers. Keep
stable per-invocation lexical contexts, local/cell views and generator state.
Allocation percentages alone are not CPU attribution; this is one falsifiable
runtime trial, not a predicted speedup.

Concrete replacement: retain one instruction driver per public/native-callback
entry or generator. Ordinary executable calls save caller control into one
capacity-admitted value-record array instead of allocating another large driver
and linking _caller. Existing ExecutableFrameState views keep paused lexical locals/cells, live
operands and instruction positions stable. Remove child-driver construction/object-switching and the
_caller chain. Keep direct driver fields for active dispatch, existing sync/async
loops, binding, stacks, function contexts, slot/cell semantics and protocol routes.
Save ALL pending calls, exceptions, block/instruction indices, returns, generator
and async-disposal state; no pool, shared locals buffer, lazy lexical context,
shared-loop replay, interface framework or alternate backend.

Use the existing governor for conservative record-array capacity/replacement
admission (512 bytes/record plus array metadata). Clear popped records; keep old
capacity funded through replacement and actual users. Ordinary entry cleanup
releases records after unwind; generator capacity stays funded through suspension
until actual completion. Frame views admit160 bytes before allocation; generator estimates include that
retained view. Their actual operand stack and instruction position remain together,
preserving exact paused-parent source spans and existing operand-lifetime checks. No new budget, owner, tracing, counters or configuration.
Earlier rejection from admitted capacity is an intentional documented tradeoff.
The first focused run exposes21 tests that inspect actual operands through the
frame; preserve their independent liveness/tail-clearing assertions. Keep operand
state in the stable frame rather than deleting these checks or adding test hooks.
This source-level correction precedes timing; acceptance thresholds stay frozen.
No refund of retained storage, cancellation shortcut or caught-admission recovery.

Acceptance before timing: existing direct/named/bound calls, defaults, recursion,
tracebacks, nested callbacks, super/locals/globals/nonlocals/closures and inline
comprehensions; generators send/throw/yield-from and pending async iterator cleanup;
synchronous and genuinely suspended asynchronous host protocols; cancellation,
first-origin refusal before effects, retained aliases/results, shared budget reuse
and zero cleanup balances. Preserve managed stack admissions, operand copies and
awaited users. Add a regression only for a real observable coverage gap. Focused
Release on both actual runtimes; no private field/count tests or artificial fuel.

One<=60s before/candidate/original-control service, normal GC/tiering and identical
auxiliaries/shared CoreLib/hash witnesses. Complete warm maintained CSV helpers,
text helpers and unaffected JSON processing;128 interleaved warmups/image,2s
settle/case, seven rotating32-job batches. Full fixture/I/O/output/file goldens,
result disposal, zero balances, quiet gates and owned cleanup remain inside their
existing boundaries. Both original medians within3%, IQR<=10%, batches>100ms.
Retain for performance only if BOTH affected scripts improve>=7.5% against BOTH
originals and unaffected JSON regresses<=3%; no phase subtraction/CPython ratio.
Preserve every adverse/inconclusive sample. One candidate, no favorable retiming,
threshold edits, profiling campaign or marginal follow-up tuning. If semantics or
these fixed gates fail, restore original production source, fresh-build/test it,
retain independent useful regressions/evidence and stop this redesign.

Full Windows/Ubuntu ordinary/original stress/package checks apply only if retained
at the next coherent delivery. B59/B60's current full checkpoint continues
independently; no repeated historical qualification or producer polling.

