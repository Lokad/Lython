# Executable work-state foundation — 2026-11-06

R13 A groups code, locals/cells, operand storage and instruction position in one
inline ExecutableWorkState. The normal executable engine uses it directly inside
the existing lexical ExecutionContext. ExecutableFrameState and its three
pass-through constructors disappear; no extra heap object, caller record, arena,
pool, execution route, owner, policy or public API is introduced.

Removing inheritance exposes two methods that previously hid one another. The
slot-only reader is now explicit (TryReadExecutableLocal); TryResolveOwnLocal
retains lexical/inlined-scope behavior. The original duplicate-definition build
failure is preserved. White-box probes read actual work-state arrays and retain
live input/reference identity, tail clearing, weak lifetime and cleanup assertions;
no legacy field-name compatibility accessors or replacement layout-only tests.

Release solution builds both targets, zero warnings/errors. Focused checks pass
**1,593 internal / 4,200 public cases per actual .NET11/.NET10**, including direct
calls, scopes/classes/imports/annotations, closures/generators, managed depth,
first refusal/cancellation, real pending operations and retained storage/reuse.
No assertions/timeouts/stress scales were relaxed. All four test core copies match
the corresponding built core hash in validation.json. Native-return and direct
UTF8 formatting remain in use.

This is a foundation, **not a performance result or completion of R13**. No new
context-skipping path is active, no isolated timing/allocation claim is made, and
full platform/stress/package delivery is batched at the coherent B boundary.
Previous terminal42208020544 qualifies runtime88f12099 only.

The selected next source proof shares instruction handlers with a standalone
work state and materializes a real context before actual scope/protocol/async work.
It must adopt current locals/IP/ownership rather than rebind or restart, leave
module/closure identity unchanged, preserve native-return gains and admit all
storage/overlap before use. If that requires a parallel dispatcher or broad new
lifetime machinery, refine/reject it and remove unused prototype foundation.
See contract.md for semantics, comparison controls and the completion decision.
Publication remains postponed.
