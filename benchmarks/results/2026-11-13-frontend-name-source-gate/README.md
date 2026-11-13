# Frontend identifier source and allocation gate

2026-11-13, committed40d92aef/frozen91d0540d. No production change or new runtime
prototype. Reuse six representative sources and the actual core through its existing
white-box access. Current complete-compilation measurements remain separate from
this short Windows allocation diagnostic; no CPU share or elapsed gain is inferred.

| Script | Parser allocation B | Whole compilation + cleanup B | Modeled duplicate identifier B / whole compile |
| --- | ---: | ---: | ---: |
| File search |18128|86488|400 /0.46%|
| Captured text |37040|135288|872 /0.64%|
| Report |37520|112616|464 /0.41%|
| JSON-lines |38568|135752|840 /0.62%|
| Invoice |61952|242888|2296 /0.95%|
| Config |43952|100152|416 /0.42%|

Warmed128times,256measured synchronous invocations per phase, current-thread
allocation counter. Parser reuses token storage; full public compilation constructs
tokens/analyses/lowered/executable code and disposes its compiled owner under a
shared16MiB host allowance. All compiled scripts are valid and every allowance
balance returns to zero. No guest execution, forced GC or runtime instrumentation.

The model prices one normalized string per identifier token against one per distinct
spelling. It is **not measured name allocation traffic, a bound on all possible
savings, or a CPU percentage**: repeated lookups of one token and mangled names are
separate, and a cache adds its own storage/work. Actual full allocation is measured;
the duplicate-string bytes are only a source-based model.

**No identifier cache selected.** The modeled storage is incidental relative to full
construction, and current evidence does not establish normalization as a dominant
CPU cost. Preserve Unicode/NFKC/private naming and avoid adding a table on that
premise alone. This is a no-change decision, not a permanent prohibition if later
evidence supports a combined representation simplification.

Reuse[R158's distinct flow facts](../2026-11-09-frontend-flow-gate/README.md) and
[R212's source review](../2026-11-10-frontend-source-refresh/README.md). Name, abstract
value and deferred annotation passes have different semantics; do not merge or skip
them blindly. Current scope/directive and container-effect walks still do real
work. Small set-copy/callsite-record redundancies do not establish a substantial
new frontend architecture. A future consolidation must remove a complete retained
representation or repeated construction stage with preserved diagnostics, lazy
scope behavior, cancellation and admission.

[Raw inventory](observations.json),[source proof/decision](source-proof.json),
[private diagnostic source](diagnostic.cs),[build](build.log).
