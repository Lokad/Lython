# D57 instruction-loop exception boundary, selected source gate

Retained source b69a0a49 (documentation f8ebdc7c), frozen86cea356. D56 full local
4131internal/16004public per actual runtime pass; CI42065949621 running. D54
42061776662 GREEN/all8 cached, never requery. No active VM or runtime prototype.

Demonstrated: one shared synchronous/completed-async instruction prefix contains
the three Python routing catches inside each loop iteration. D52 prefix157/723
text and125/1017ordinaryCSV self samples contain actual opcode work, NOT a
removable fraction. Hypothesis only: a protected inner loop with cold routing
outside can improve generated loop code/register use. CLR EH is not a counted
per-instruction operation, so do not claim removal of runtime try-entry costs or
predict a speedup. This is a compiler/code-shape hypothesis needing measurement.

Critical distinctions from actual source:
- Entry CheckExecution currently sits before the protected instruction body.
  Its cancellation/terminal refusal escapes without TryRouteRuntimeException's
  PythonContext/error-handler selection. Operation-thrown failures DO route.
- Every successfully routed catch already has jumped=true. Return may exit
  immediately; failed control/runtime routes throw. Pending disposals and frame
  return are checked after routing; no instruction increment after a caught route.
- Helpers can change _instructionIndex before throwing; catch must keep the
  originating instruction reference/span rather than reread the changed index.
- Incomplete operations retain original pending span and return Operation;
  pending cursor cleanup returns Disposal with the actual jumped state. Both
  remain outside a new driver/framework and keep their existing ownership.

Candidate for a bounded prototype:
Keep outer retry loop for routed errors, an instruction ref local and existing
jumped local. Place normal instruction iteration + existing success tail inside
one try. Reassign the same ref at each instruction start; no separate hot span/
phase state. Existing catches outside the inner loop route with that ref. On
routed errors run only existing error-tail disposal/frame-return checks and retry
the outer loop; there is never a normal increment there. A small duplicate cold
tail is acceptable; do not add a per-instruction virtual/helper dispatcher.

At instruction entry test sticky governor AdmissionFailure or active token
IsCancellationRequested. If true, branch OUT of the protected region to existing
CheckExecution at the originating span; it retains first-refusal priority and
original uncaught entry behavior. Both predicates are sticky, so no recovery or
work can occur between that branch and the actual existing failure. No allocations,
callbacks/finally or new counters/flags/exception policy between them. Normal
instructions keep effective admission/token checks. No additional time authority.

This placement proof still needs actual C# compilation plus independent semantic,
token/refusal/lifetime validation; it is not a prototype or measured improvement.
Stop if keeping span/entry-vs-operation routing needs extra hot stores, phase flags,
another dispatcher, control-record stack or replaced execution strategy. Do not
rescue by silently weakening cancellation or altering PythonContext attribution.

Before code freeze an unchanged baseline and write exact patch/contract. Focus
both APIs and actual pending callbacks, finally returns, with exits, nested
errors, generators/send/throw, terminal suppressed refusal and cancellation,
active-borrow cleanup and budget reuse. Existing tests first; add a case only for
a demonstrated gap. One short complete CSV/text/JSON/report +basic-loop source
pair with identical-current controls and normalGC/tiering; compilation separately
if affected. Existing quiet/source/fixture/golden/library/Closed/finalzero/join/
removal gates, bounded comparison/correction, all adverse conditions retained.
Retain/refine/withdraw with explicit debt, then coherent full platform/stress/
installed qualification. Broader call/scalar/frontend and regex tracks continue.
