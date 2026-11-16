# R456 prospective native-block migration

Private baseline9e8d1cf5. Main production is unchanged by this assessment.
See source-obligations.json for the30 current eligible opcodes and exact inputs.
The frontend whitelist matches the current native switch. This is a complete
replacement target, not an arithmetic microkernel or a production JIT claim.

Hypothesis: emitting whole helper control flow over current managed slots can
remove repeated opcode selection/decoding. It does not remove Python binding,
useful arithmetic, admission or real stack checks. R452's overlapping samples
cannot predict a gain. New-script compilation must pay for itself; R447's tiny
managed API probe already established feasibility and nontrivial first/warm costs.
Do not repeat that unchanged feasibility measurement.

Current facts prevent false simplifications:

- Slots already contain one reference and one long; no BigInteger field/universal
  wide union needs replacement. Instruction storage is already compact.
- Ordinary arguments bind into final slots; receiver arguments view their source.
  Native buffers have16 managed slots; wider/pending work is already owned.
- ExecutionFrame dictionaries are lazy; immutable builtins/startup workers are
  shared. Do not rebuild completed setup and namespace optimizations.
- EnterInterpreterFrame, EnterFunctionCall and native-entry stack validation serve
  distinct entries/depth/error order. Preserve the validated policy; do not infer
  redundancy from adjacent code or zero samples.
- The R439 shared interpreter kernels measured slower after several refinements.
  This proposal eliminates runtime selection through prepared control flow; it
  must not reconstruct their full-state initialization or generic dispatch router.

Expected retained change: a code-owned, strongly typed managed delegate taking
ref NativeExecution and out ExecutableSlot. Compile one complete native schedule
for eligible functions; keep the existing binding, NativeExecution lifetime/Run
wrapper and ordinary executable continuation. Remove RunCore's switch and the
duplicated IsInlineInstruction whitelist if the emitter establishes eligibility.
Keep real Call/Format/Read/Publish/storage helpers. No per-instruction delegates,
new generic frame carrier, raw IL, unsafe API, package, counter, tiering policy or
public execution mode. A partial private emitter is never a delivery endpoint.

Control-flow obligations:

- Every original PC/span and token/refusal check precedes its actual work.
  Publish can consume an adjacent local store; its check/span must survive.
- Closure arithmetic and cased split can skip instructions. Generate their known
  successor alternatives; avoid replacing one opcode switch with a runtime PC
  dispatcher on every operation. Rebinding still selects the ordinary semantics.
- Conditional jumps, iterator exhaustion and abrupt exits retain cleanup order.
  GetIter declines before user protocols; ForNext owns the acquired cursor.
- MakeTuple can complete an unpacked return directly. No target is visible before
  the original cleanup/admission checks; identity publication remains canonical.
- NativePrefixEnd is a physical finally frontier, not only lexical fallthrough.
  Leaving it transfers before cleanup; handler-only instructions are never native.
  Unexpected control flow is a compiler defect, not a silent alternate execution.
- Call may prepare a child and then transfer. Keep its exact advanced position,
  funded child/operands and failure provenance; do not replay argument effects.
- Catchable faults transfer once at the original PC. Cancellation and first
  refused admission remain terminal even through guest suppression.

Ownership/preparation gate before implementation is promoted:

- Reuse the existing compiled/execution owner for substantial retained code and
  construction scratch; reserve rough major capacity plus deliberate framework
  headroom before building expressions or compiling. No new memory subsystem.
- Prepare once during the corresponding common compilation path, including
  imports, with the current token. A cached delegate retains code metadata, never
  a caller token/context or temporary span. Overlapping executions share the
  immutable code and their own execution state; escaping users keep its owner.
- Compiler/JIT work is synchronous and joined. Checks around framework calls do
  not prove interruption inside them. Quantify/document that limitation rather
  than launch abandoned background compilation or add a watchdog.
- Standard managed expression compilation requires assessing dynamic-code/AOT
  behavior. No current AOT promise was found in root/current docs. Do not silently
  substitute the expression interpreter, claim new platform support, or hide
  framework failures by falling back to the displaced native engine. Establish
  an explicit compatibility decision before promotion.
- Existing instruction storage remains necessary for semantic continuation.
  Generated code increases retained compilation storage; do not claim its deletion
  or physical CLR reclamation when logical ownership ends.

Validation/completion:

Cover all30 current native opcodes and their fused successors before deciding.
Use existing Python semantics, exact spans, integer identity/overflow, closure,
default/protocol, protected cleanup, cancellation/refusal and pending-user tests.
Require shared-budget baseline reuse, retained results/code funding and no replay
through actual I/O under both public APIs. Preserve the existing normal engine.

Then compare complete numeric/report and helper scripts, warm and newly compiled
once, with normalization/control scripts. Include construction/first invocation
and final code-owner cleanup in the proper phases; first framework startup is
separate and visible. One bounded quiet-VM source pair initially, with controls
and all adverse originals retained. Refine on justified evidence, not to obtain
a preferred sign. Explicitly decide remaining regression/complexity debt before
full platform/stress/package delivery. Another isolated feasibility result does
not complete either this migration or the whole-script objective.
