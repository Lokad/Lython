# Bounded containment scope and immutable regex assessment

The next milestone has four complete scripts, explicit correctness/retention/
cancellation gates, embedding-latency targets and stopping criteria. The private
active PLAN was condensed; its entire preceding history remains in a separate
private, hashed archive. Completed stack, storage and async-thread fixes remain.

Lython's lasting managed-only constraint is recorded in AGENTS.md. Unsafe
compilation is explicitly disabled. Dictionary replacement now uses managed
ContainsKey/indexer operations instead of CollectionsMarshal and Unsafe.IsNullRef.
This may add a lookup; no performance change is claimed. Allocation preflights,
live-storage charges, forced-relief policy and managed stack admission are preserved.

## Complete scripts and validation

[The four scripts](../../scripts/README.md) perform CSV conversion/filtering/
aggregation with quoted multiline Unicode records, Unicode text counts, streamed
JSON-lines aggregation, and directory/file copying with a deterministic manifest.
Public tests compare exact published file bytes and stdout for empty, 16- and
1,024-repetition fixtures in Run and genuinely delayed RunAsync under a 256 KiB
execution cap. Four cancellation cases interrupt actual host reads before output.

The same sources pass twelve independent CPython 3.13.2 fixture/output checks,
including binary bytes and Unicode filenames. The verifier preserves source,
input and output hashes. Only standard-output text line endings are normalized;
published file bytes compare exactly. These are correctness checks, not timings.
Fair timed comparison still requires file staging/output validation in the
comparison worker; no ten-minute lane or quiet-VM collection was run.

All 10,415 Debug tests and 100 focused Release checks pass. Exact source, binary and receipt identities are recorded in evidence.json.
The focused Release gates cover the scripts, dropped/retained CSV aliases,
cancellation, dictionary denial/accounting, generation relief, managed stack
admission, cycle/projection and reclamation invariants.

## Supplied immutable Utf8Regex pair

Frozen candidate: core/PythonRe **0.4.0-scalar.2**, Release/net10.0, producer
**ebae61ba1e6c48525eca9e0c5a5ce53142440d10**. Both packages, symbol packages and
DLL hashes match the supplied immutable manifest; exact identities are in
evidence.json. No sibling production source was edited or rebuilt.

Detached checks pass **24/24**: capture order/completion/backreferences, four
finite-policy constructors, work/depth/working/result denial, persistent cursor
progress/aliases/disposal, retained snapshots, cancellation between advances and
shared work exhaustion. This does not qualify cancellation at every backend
checkpoint or all construction/getter/retained-memory exclusions.

Lython's current adapter builds against the exact pair. Its 61-case capture
corpus matches **48/61 in both modes**; all thirteen differences concern its
obsolete lastindex/lastgroup heuristic. The package's authoritative metadata
passes the direct controls. Lython must consume it and integrate finite invocation
controls and incremental finditer ownership before promotion.

Promotion is also blocked by the lasting managed-only constraint. The frozen DLL
metadata references Unsafe.Add/As/AsRef/ReadUnaligned/SizeOf and vector LoadUnsafe
operations. The Utf8Regex-centric handoff was refreshed with exact artifacts,
passing controls, this blocker, scope exclusions and a request for a new immutable
managed implementation. The production pin remains PythonRe **0.2.0**.

## Explicit remaining milestone gates

Forced CLR collection remains an architectural dependency. Removing it alone
previously caused false MemoryError on bounded streaming. Younger-generation
relief and fewer full collections do not prove acceptable embedding latency.

The prospective experiment compares unrelated managed host requests/heartbeat
with host-only controls, records p50/p95/p99/max, heartbeat stalls, allocation,
GC and CPU, and checks complete workload outputs/retention outcomes. Targets are
added request p99 <=5 ms and added largest heartbeat stall <=25 ms, plus no
repeated Gen2 relief in bounded streaming and bounded pinned denial retries.
These are proposed engineering acceptance targets, not current guarantees.
Freeze one comparison matrix, at most one owner-release candidate and three
independent <=30-second samples per entry; publish failure or uncertainty and stop.
The milestone stays open until measured embedding impact and the remaining
cross-platform containment gates pass. Full GC independence stays separately
explicit until forced relief actually disappears.


