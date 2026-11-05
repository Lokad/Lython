# D57 protected instruction loop, before implementation

Baseline f8ebdc7c, same production b69a0a49/frozen86cea356. Build/qualification
of unchanged D56 source already recorded; do not infer binary identity from later
commit metadata or repeat its terminal qualification. Source gate/source archive
and every adverse result remain separate from a CPU attribution claim.

Implement exactly one normal protected inner instruction loop and existing three
routing catches outside it. Existing outer loop only re-enters after routed
failure. Keep one instruction ref across catches, reassign at each instruction
start. Existing jumped local stays within successful iteration; all successful
routed catches previously set jumped=true, so cold disposal tail uses true and
never increments the routed target. Retain existing return/control/runtime routing
helpers and pending-operation/span/disposal drivers; remove the old per-iteration
catch placement, not Python behavior or the executable engine.

Instruction entry: sticky AdmissionFailure or current token cancellation branches
outward to existing CheckExecution at the originating span, outside routing.
No operation/allocation/callback/finally between predicate and failure. Refusal
still wins. No token cache, counter, phase flag, added hot state or public API.
Operation-thrown errors remain routed with original instruction even if the helper
already changed _instructionIndex. Existing normal tail is plain field/ValueTask
construction/index update, with no guest/host/failable resource operation; putting
it inside the region does not introduce Python-thrown error routing.

Same scope, slot/operand storage, owners, clearing, class cells, closures,
generators, yield/send/throw, actual suspension and awaited cleanup. No new stack,
dispatch backend, frame carrier, admission retry, allowance/cap/time authority or
unsafe code. A small duplicated cold disposal/return tail is an explicit tradeoff.
No runtime try-entry-cost claim: hypothesis is JIT loop layout/register allocation.
D52 prefix samples include real opcode work and are not removable CPU fractions.

Source acceptance before measurement: compiler accepts ref/cold-branch placement;
existing exceptions/finally/with, execution boundaries, genuinely delayed operations,
refusal/cancellation and shared ownership regressions on actual11/10. Inspect a
demonstrated test gap before adding a test; no checkpoint-count mirrors or relaxed
timeouts/containment. Stop if this needs phase flags, hot span copies, another
dispatcher/control stack or altered entry-vs-operation routing.

One <=180s comparison: complete ordinaryCSV/text/JSON/report warm and basic loop,
same workerb94/CPython3.13.16/current-control and frozen86cea356. Compilation source
unchanged; do not replay an unchanged compile-only gate. NormalGC/tiering, current
quiet/source/fixture/golden/library/Closed/finalzero/join/removal gates. Keep all
adverse and inconclusive conditions, allow bounded justified confirmation or
correction, no fixed-percent veto. Separate simplification/elapsed/allocation/
causal CPU. Retain/refine/withdraw explicitly; coherent full delivery if retained,
or nonincremental restore/build before checks if withdrawn. Continue roadmap.
