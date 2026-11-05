# D42 executable target family: candidate design gate

2026-11-05. Source gate during D41 collection; no production prototype/timing yet.
Do not infer a speedup before the retained whole-script checkpoint and comparison.

Current e958 target execution passes ApplyOperation -> prepared-operation type
switch -> always-async target-operation type switch. Delete additionally retains
and switches on assignment syntax. Both Run and completed RunAsync use this.
Prior B248 text capture has target-state-machine27/1108 leaf samples, plus common
dispatch/setitem work; these are neither current CPU attribution nor additive
removable shares. D36 allocation is separately sampled, not CPU. Existing ordinary
calls already have a synchronous prefix: do not claim they await every native call.

Candidate coherent replacement: represent the complete assignment/read-retained/
delete/unpack/assert/raise family as ordinary existing executable instructions.
Remove its nine ExecutableOperation record types and retained delete AST, target
default in the generic prepared dispatcher, and unconditional async target/delete
state machines. Reuse existing instruction primary/secondary/storage operands,
name/constant tables and main executable driver. No new dispatcher framework,
saved activation record, call arena, value tag system or memory infrastructure.

The two source spans are real: keep instruction guard/routing Span and store
TargetSpan distinct. Existing source-span objects can occupy the constant table;
member name plus target-span index fit existing two operands. Separate ordinary
and augmented store opcodes carry ValueLast without another metadata object.
Unpack target count/star index fit two operands; slice deletion uses SliceParts.
Retain conservative local-forwarding invalidation initially, including deletion;
do not silently strengthen dataflow facts while moving the representation.

One non-async target executor uses actual synchronous helpers for Run, completed
operations until suspension for RunAsync. Incomplete value/void operations keep
all inputs and funding, then publish/retire at the same completion boundary.
Reuse existing completion conventions; eliminate the unconditional state machines,
not duplicate the entire evaluator. Async descriptors, hashing/equality/indexing,
iteration and unpacking can reach host I/O; exact builtin fast completion must not
hide those callbacks. No early child/call work before queued cursor cleanup.

Affected maintained workloads: text count updates, ordinary CSV aggregation,
JSON aggregation/config edits; report is a broader control. Acceptance: original
RHS/receiver/key order; receiver/key evaluated once for augmentation; exact slice
object retained through read/store; Python errors/source spans, missing names,
starred unpack cardinality and partial stores; closures/generators/class bodies;
real in-flight protocol cancellation/refusal; pending borrows funded through
awaited cleanup and baseline reuse. Preserve public regressions; replace only
white-box assertions naming removed record classes with meaningful opcode/behavior
coverage. No test-only checkpoint/fuel assertions.

Before code: use D41 remaining gaps to select or reject this milestone and verify
all instruction consumers/stack effects/terminal edges/native-body rejection.
If selected, finish the family deletion, focused Release semantic/ownership/token
checks, then one <=180s frozen whole-script warm/fresh/async controlled pair with
two unchanged roles and unaffected controls. Preserve adverse/uncertain points;
explicit retain/refine/withdraw and regression-debt decision. Allocation/layout
simplification is distinct from CPU/elapsed improvement. Full platform/stress/
installed consumers batch at the next coherent retained checkpoint.
