# Call operand forwarding after withdrawal

2026-11-12; all 568 production hashes restored to qualified da9e5066. This is a
bounded source assessment, with no new timing, CPU forecast or prototype.

Existing `ExecutableStackLayout.ForwardValues` already removes adjacent constant
and uncaptured-local loads for prepared value instructions, retains original
unbound-read spans, and forwards final results to local stores after retirement.
Native helpers already borrow exact positional operands or bind live defaults/
keywords into managed inline slots. Ordinary frames already use the admitted
binding array as their local/temporary workspace and adopt actual continuations.
There is no duplicate general frame or argument array to delete here.

General call instructions still consume contiguous evaluated operands. Extending
value forwarding to arbitrary call arguments cannot simply reuse NativeInput:
its native expression-node indexes and ordinary physical slot indexes are distinct
domains, prepared before and after forwarding respectively. A second mapping alone
adds metadata; the current binders still require the argument span. Deferring reads
from mutable locals/cells until binding also changes evaluation order when later
arguments, keyword defaults, descriptors or admission callbacks mutate them.
Actual suspension requires stable funded snapshots. Per-arity direct invocation
patches would leave the broader ownership/control construction intact.

Stop this bounded variant before implementation: no complete preparation or
representation stage has yet been shown removable. A coherent future call/body
migration must share prepared operation/input facts and preserve the native
route's avoided dispatch, or change binding/operand transport end to end. It must
name the displaced representation and exact effect/suspension frontier before
prototyping, with no second evaluator, generic accessor facade, caller-window
arena or silent native-path regression. R268 is not a permanent design ban.

CSV/report source proof already excludes replaying row adoption, UTF-8 output,
integer formatting and shared workspace as new improvements. Independent frontend
and regex work remains available; missing producer publication is not a blocker.
The overall performance objective remains open.
