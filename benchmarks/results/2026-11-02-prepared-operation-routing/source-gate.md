# B114 complete per-record dispatch/storage gate

B113 is committed b6a75be3, delivery CI41472979014 active. No new runtime
prototype. Its text/report benefits and accepted CSV/JSON guard regression debt
remain explicit. Do not replay prior frame arenas/dispatcher or add tiny native
return cases. Performance objective remains open; no claim from source alone.

Source fact: synchronous ApplyOperation calls nonasync ExecutePreparedOperationAsync,
whose default enters async ExecutePreparedSuspendingOperationAsync, whose default
awaits async ExecutePreparedDefinitionOperationAsync, whose default awaits async
ExecutePreparedTargetOperationAsync. A normal dictionary subscript store passes
through three state-machine bodies before SetSubscriptValue, even under Run.
PreparedTargets contains real pending protocol operations and must retain them.
Native dictionary/key/hash/equality semantics and explicit storage ownership
already exist; do not replace them or assume all dict access is protocol-free.

Hypothesis: route prepared operations once to their actual existing handler,
removing chained default async forwarding/state-machine initialization on every
record. A later coherent family cleanup may avoid synchronous completion through
the remaining async body, but only if measured evidence supports it. No new
dispatch framework, backend, modes, native-type matrix or cached guest policy.
Expected deletion: intermediate default-await routing and its unnecessary
state-machine work; preserve one operation implementation and operand cleanup.

First bounded read-only step: reuse checked worker/perf199Hz/frame-pointer/CLR
maps for exactly1024 complete CSV and1024 text warm jobs, ordinary GC/tiering,
profiling-only PerfMap override, <=30s captures and owned90s service. Shared VM
lease, quiet before/after, full source/stdout/files, actual named guest thread
CPU delta (not process max-CPU heuristics), workers joined and fixtures removed.
External profiling adds no unsafe/native production code. Preserve unknown and
inlined samples; inclusive async frames are not a removable CPU fraction.

End this gate with an actual source-backed elimination choice or a different
dominant-cost hypothesis. If selected, semantic acceptance covers sync/async
assignment, augmented and chained targets, slices/delete/unpack, evaluation and
exception/finally order, real protocol/host suspension, terminal refusal,
cancellation and operands/aliases funded through awaited cleanup. Then one
coherent prototype and controlled complete CSV/text/JSON/report comparisons with
separate compile/fresh/warm boundaries. Explicitly decide any remaining B113
regression debt and remove displaced routing. No speedup prediction or broad
ownership/boxing audit.
