# Current text execution cost gate — B146

2026-11-03. Qualified runtimef7dd0d3a, exact core2f78a866, protocol2 worker02efd797.
One fixed initial perf cpu-clock/fp capture of the complete text-summary helper
script,2048 invocations. Actual active guest thread3029799,6.93s CPU,1377 samples.
Ordinary GC/tiering; disclosed DOTNET_PerfMapEnabled=1 for names. Profiling elapsed
is not a performance comparison. [Exact identity](identity.json), [request/goldens/
thread CPU](record.json), [leaf attribution](guest-cpu-leaves.json), [call chains](guest-call-chains.json),
[native origins](native-origins.json), [complete raw capture](raw-capture.tar.gz).

| Observed work | Samples / share | Interpretation |
| --- | ---: | --- |
| ExecuteUntilCall leaf |302 /21.93%|Includes inlined work; not all removable dispatch.|
| Native helper call route inclusive |456 /33.12%|Includes useful lower/split; not function-frame overhead alone.|
| Casing inclusive |165 /11.98%|Current native UTF8 pipeline; old per-rune CLR conversion is already gone.|
| Whitespace split inclusive |146 /10.60%|Existing bulk admission, fragment arrays/copies and result construction.|
| GuardNativeReturn inclusive |43 /3.12%|Does not justify attributing the whole helper cost to repeated guards.|
| Monitor enter/exit leaves |97 /7.04%|Mostly admission/storage paths; overlapping callers.|
| Exact integer addition leaf |30 /2.18%|Not evidence that boxing is the dominant cause.|
| Unknown leaves |70 /5.08%|Explicitly unresolved; one incomplete terminal map line preserved.|

These rows overlap. No sum or predicted speedup is justified. Compared with the
older830c6251 text profile, current source has already removed substantial casing/
conversion work. Do not revive a universal boxed-value rewrite or old call-arena/
control designs on this evidence. Normal calls already bind into final locals;
helper returns already omit resumable function frames when guarded semantics permit.

Selected next design gate: coherent compiled operand transport. Remove redundant
load/push/pop/store dispatch around ordinary expressions, preserving eager operand
capture across callbacks and existing call argument spans. Use the existing engine,
shared frontend facts and admitted frame workspace; no second VM, runtime type-
specialized opcode catalogue, call arena or options. Prototype/source criteria and
whole-script controls must decide retention;21.93% is not a promised gain. General
immutable UTF8 slice storage remains a separate candidate: it could remove fragment
arrays/copies, but global object-size and retained-backing ownership must be proved
before implementation. No storage view/pool/owner has been introduced here.

Before/after quiet gates and complete stdout/files PASS, worker/profiler joined,
owned service16.856s/exit0/inactive, fixture removed, lease free. The first archive
attempt omitted root-owned perf.data and included FIFO metadata; that partial
retrieval remains private. The existing perf.data was made readable and the same
completed capture retrieved intact; no recollection or discarded adverse samples.
Publication remains postponed. The performance objective remains open.
