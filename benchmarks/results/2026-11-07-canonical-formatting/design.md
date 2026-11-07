# R26 bounded source gate and correctness correction

Baseline01aedc3b. Current compile-only source inspection confirms runtime native
proof/evaluation traversals, and a focused regression demonstrates a raw CLR
InvalidCastException after substantial formatted-output admission changes a later
global method receiver. Original source/build/test/stack are retained. This is a
demonstrated correctness defect; no guessed CPU cause or elapsed result.

General single-pass promotion is larger than a cheap guard deletion. Existing
ContextRequired promotion owns live flat operands; recursive native evaluation
does not retain earlier forwarded locals or an executed-instruction journal.
Formatted evaluation also owns a builder across prior field effects. Arbitrary
handoff would need replay-free slot/binding/builder transfer and actual async
callbacks, not restarting the function or freezing reads before admission.
Do not start that wider state/journal design automatically or add another arena.

Selected bounded alternative: remove the duplicate full formatted-body native
execution route and its builder-spine metadata/proof/evaluator. Existing executable
formatting already uses the same admitted direct-UTF8 kernels, live slots, true
async protocols and cleanup. Keep primitive native-return helpers and direct
single-value formatting/conversion. During existing native preparation, decline
mutable global/name/subscript reads after an earlier potentially allocating
operation. Parameter/constant snapshots and intrinsic results remain eligible;
later mutable lookup semantics use the canonical executable engine before work.

This removes machinery rather than adding promotion state. No Python feature,
resource guarantee, public option, budget policy, new dispatcher or weak/GC
mechanism. Existing native helper return/UTF8 formatting kernels remain. Full
formatted helpers can cost more frames/allocation; measure and explicitly decide
remaining report debt. Correctness cannot be restored by reverting the known bug.

Acceptance: changed receiver and late global read after admission, exact effects
once/current lookup order, actual asynchronous operations and cancellation, first
refusal, aliases/funded input and builder cleanup, frame/source errors and Python
locals. Existing primitive/forwarded native returns, direct number/text formats,
compiler facts, closures/classes/generators remain. Focused Release both runtimes,
existing short whole-job allocation plus bounded report sync/async, text and JSON
controls. No automatic full qualification replay for unchanged earlier milestones.
