# Operand/lifetime deletion proof, 2026-11-12

Exact production da9e5066, source unchanged during this investigation.

CreateExecutableWorkState does not allocate a second frame or parameter copy.
For ordinary deferred helpers, its stack wraps bound.Values; locals alias that
array. With no captures or match storage its frame-storage reservation is zero.
The returned work is a value type. The existing workspace loan is already reused.
Removing another wrapper around this state would not remove the underlying work.

A stack span cannot be kept in ExecutionContext across genuine suspension.
Borrowing a caller subrange with the current object[] convention needs either
window offsets at every access or a changed synchronous-prefix representation
with export of live operands at the exact suspension/protocol point. The earlier
arena trial already paid added slot/save/restore costs and measured slower.
There is no proven deletion here without a materially different transport design.

The native proof is a postorder operation graph, but guard/evaluation recursively
read that graph with dynamic Python type relations. A flat proof needs temporary
shape storage; limiting nodes would add an execution cliff, and allocating scratch
before type eligibility changes admission/host-reentry timing. Merely exchanging
recursion for another proof vector is not a selected coherent simplification.
Existing code must not be removed until its replacement preserves native gains.

Report fields add another hard requirement: allocation during an earlier format
may reenter and alter the record before a later field read. Snapshotting every
field up front or restarting after an effect is semantically wrong. A design
must keep the exact ordered continuation and input funding.

This closes this bounded source proof without a runtime prototype. No CPU gain,
permanent design veto or completed performance objective is implied. Preserve
full native code, the ordinary resumable prefix and all ownership/cancellation
checks. Use the fresh external checkpoint to prioritize the remaining complete
script gaps; do not automatically add a carrier, universal tagged value, guard
hierarchy or second expression engine from sampled leaf shares alone.
