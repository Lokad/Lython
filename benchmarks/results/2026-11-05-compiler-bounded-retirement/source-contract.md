# D56 compiler-bounded operand retirement, before implementation

Baseline0752be3c, D55 frozen7895ac6d. D54 full actual runtimes passed, platform
42061776662 still running. D55 focus passed; possible Unicode+.20/2.18% retained
as debt. Do not present either as a completed performance objective.

Current RetireTo examines every retiring operand for Cursor/private temporary.
D49/D52 actual disposal/retirement samples justify source work, not a removable
CPU share. A real static distinction exists: ordinary guest values cannot be any
of the four internal temporary classes. They never enter guest locals, arguments,
return values or public host projection as these internal wrappers.

Audit of all private producers/transport:
- GetIter: synchronous ExecuteValueOperation and completed/pending async acquisition
  replace InputStart with Cursor; native list/tuple iteration may keep a guest value.
- Generator-expression initial depth1: SeedGeneratorOuter puts Cursor in first
  temporary slot. Ordinary generator functions start with no seeded operand.
- StartComprehension: InlinedComprehensionScope at ResultSlot. Result collection
  and original outer guest iterable occupy the next two slots; GetIter marks cursor.
- StartCall: accumulator replaces InputStart; StartClassHeader pushes accumulator
  at ResultSlot; expanded completion consumes it, retaining awaited cleanup.
- StartFormattedString: builder at ResultSlot; finishing converts to guest string.
- Dup: mark its duplicate output conservatively regardless of actual guest type.
  Other raw slot writes are guest results, sent None, slice keys or cursor writes
  already above. Context-manager records have their existing separate handler
  lifetime and were never ExecutableTemporaryValue. No other private subclasses.

Select ONE conservative highest possibly private-owned physical slot, computed
in the existing reachable shape pass alongside depths/physical slots. Include
seeded input and all Dup outputs, no new pass, bitset, array, runtime ledger,
classification mode, owner layer, allocation or policy. Store immutable metadata
on code and one integer in existing value storage; construct both ordinary and
retained generator storage with it. Value forwarding preserves physical slots.

RetireTo starts at min(LiveExtent-1,highestPossiblyOwnedSlot), keeping the current
reverse disposal loop unchanged, then Array.Clear of the ENTIRE original range.
Only the scalar guest-value classification walk above that ceiling disappears.
No disposal reordering, lifetime shortening, storage refund or async-chain rewrite.
Failed disposal still preserves original live extent/entries; pending cursors are
still cleared as they move to the owned chain, preventing duplicate enqueue.

Tradeoffs: conservative Dup/high nested owners can leave much of a scan in place;
code/frame metadata gain one integer. No claimed removal of all operand lifetime
work, argument copies, context/interpreter allocations or measured allocation
improvement. Stop if proof requires broader liveness/transport or a second ledger.
No fuel, checkpoint counters, unsafe code, cancellation weakening, refusal retry,
new owners/caps/default allowance or real-user early refund.

Acceptance: mixed guest/private retirement, reverse disposal/failure propagation,
actual pending cursor cleanup, first admission refusal, retained aliases, closures,
generators/yield/send/throw, comprehensions, expanded arguments/class headers and
formatting. Existing public semantics and lifetime/cancellation coverage first;
small white-box range proof preserves independent assertions. Focus Release on
both actual targets. One <=180s complete ordinaryCSV/text/JSON/report source-pair
comparison plus compile lane and unchanged current controls, normalGC/tiering/
CPython and established quiet/golden/identity/Closed/final-zero/join/removal gates.
Retain/refine/withdraw with explicit debt; bounded correction permitted for real
uncertainty. Batch full platform/stress/installed delivery if retained. Whole
invocation/scalar/frontend and independent regex roadmap continue after the gate.
