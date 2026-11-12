# Current call/body boundary — R264

2026-11-12. Production983bab96/unchanged796c3218, frozenb2cbfdb6. This is a
source-selection investigation, not an allocation attribution or speedup claim.
The five-script inspection compiles against the exact frozen core; code-facts
records every helper's mirroring, captures, protected regions and opcodes.

The context already inherits its namespace frame. Bound arguments are the local
prefix of one compiler-sized workspace; operands use that same backing. Completed
ordinary prefixes do not establish pending-function/receiver/caller state. Native
helper calls borrow existing operands or existing inline binding. These removed
stages must not be proposed again. Older arena/controller/shared-prefix slowdowns
remain relevant; a replacement record around the same state is not simplification.

The ordinary guarded-search helper is `if not line: return False`, followed by
`line.startswith(prefix)`, not try/except. Its seven executable instructions
include Unary, JumpIfFalse and the shared receiver call. It has no captures,
mirroring or protected region. NativeBody is absent, but the existing prefix can
perform native truth/unary/receiver operations without constructing a context.
The native-search helper has a four-instruction native body. Their different
script shapes/timings do not isolate setup cost. The remaining actual boundary
is owned binding/workspace initialization plus ordinary dispatch, not a second
frame object. Dynamic protocols still promote before observation or suspension.

CSV validation likewise keeps an ordinary body with comparisons/local stores/
tuple construction; report render keeps a formatted-string body. The report
already appends fields directly into its final UTF8 builder. Captured text's
helper already has a native body and its loop uses the native dictionary segment.
An extra native opcode cannot be presented as an end-to-end design until its
whole-call removal and semantic costs are explicit. Earlier native guard/evaluator
profiles include useful work and cannot prove today's ordinary setup dominance.

One six-second current guarded-search Run profile B610 is justified to distinguish
binding/workspace/dispatch from useful file/string work, using actual loaded code,
guest-thread CPU and all unknown samples. Existing profiler/worker,120s owner,
normalGC/tiering plus explicitly recorded map/disassembly diagnostics. No retiming,
new framework, broad workload audit or permanent one-attempt veto. Stop at enough
current attribution to select a coherent stage, or record the precise uncertainty.

If call/body setup is material, design one complete entry/operand transport
milestone across conditional/CSV/report helpers. Name displaced binding/work and
retire it where replaced; do not accumulate receiver/arity exceptions. Preserve
mutable defaults/keywords/descriptors, dynamic callable identity, evaluation and
error order, closure cells/aliases, admission reentry, terminal first refusal,
current cancellation, actual async protocols and cleanup-before-publication.
Any native proof must decline before effects, never replay after admission/work.
If useful operations dominate, choose that construction/transport stage instead.
An isolated opcode tweak does not close the performance objective.
