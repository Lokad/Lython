# Ordinary helper assessment and representative CSV coverage

2026-11-05, unchanged runtime **450b9162**, frozen core **d8d8df23**. New benchmark
corpus source/patch below; benchmark DLL **d4974271**. This is source/CPU attribution,
not a runtime before/after speedup or external CPython ratio. D31 remains focused-
qualified; its full delivery is batched with the next coherent retained change.

`csv-summary-functions.py` moves parsing/filtering/dictionary updates into an
annotated branching helper. It shares the native-helper CSV fixture and independent
full output goldens. Empty/small/large cases join the existing representative
catalog. The previous four single-expression helpers already qualify for native
bodies; do not infer ordinary frame allocations on those paths.

Compiler inventory: native parse_amount has four instructions/three native values,
five value slots/no member caches. Ordinary accumulate has24instructions/no native
body, nine value slots/four locals/one member cache. Neither mirrors local values.
Existing bound arguments already share local/operand storage; namespaces are lazy.
Ordinary calls construct context/interpreter objects, unpooled bound storage and
member-cache backing. No duplicated local array or eager dictionary is inferred.
[Inventory](census.json), [tool](census-Program.cs).

## Bounded current CPU attribution

Quiet Azure F4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering; only perf-map symbols
enabled. Existing protocol2 worker, perf199Hz/FP, six seconds guest warmup and two
seconds settle per script;1,024 captured complete jobs each. **32s owned**,45s bound.
All exact libraries/Ready identities, full independent CPython3.13.16 stdout/files,
normal runtime, quiet endpoints, Closed/exit0, joined profiler/workers and removed
fixtures pass. File-worker executable/deps/runtimeconfig are actual colocated files,
with immutable dependency links. No B250 executable-link staging error repeated.

| Current workload | Guest CPU | Samples | Unknown leaves |
| --- | ---: | ---: | ---: |
| Ordinary helper CSV |5.68s|1,131|93 /8.22%|
| Native helper CSV |5.10s|1,013|76 /7.50%|

Ordinary activation construction/lifetime has29leafsamples(2.56%); bound-storage
methods21(1.86%). These are useful work too, not removable CPU. The broad activation
group includes shared native call entry:136ordinary/137native inclusive samples;
it does not measure an ordinary-frame fraction. CSV has185ordinary leaf(16.36%)/
414inclusive(36.60%) versus149native leaf/421inclusive. Instruction-boundary
inclusive samples overlap all useful downstream work; no dispatch speedup follows.
Actual declaring owners exclude parameter-type matches. One incomplete map line
per worker and all unresolved samples remain. Lock callers identify required
funding/CSV work; they do not justify a new budget/credit policy.

**Decision:** no frame-elision/arena/context rewrite selected from this capture.
Actual frames cost something, but do not dominate the observed complete workload
enough to justify its namespace/closure/async risk. Continue at the demonstrated
collection-construction boundary: known three-field CSV rows currently allocate
eight-slot list backing. Assess removal of unnecessary backing and empty-list
replacement allocations using existing ownership, not a new record representation.

## Validation and limits

68existing public worker cases pass per actual .NET11/.NET10, including three new
corpus scales and explicit async/fresh ordinary-helper controls. Affected benchmark
and public projects build Release with zero warnings/errors; production unchanged.
Four sources match independent Windows CPython3.13.2 full file goldens; Linux
preflight checks both profiled sources with exact raw LF stdout. Initial standalone
Windows binary-stdout check expected LF and observed CRLF before any VM upload;
local verification compares the platform newline, while file bytes remain exact.
The protocol worker uses StringIO. Preserve that condition and the initial private
census-generator quoting/build failure; corrected census builds before inventory.

No RSS/startup/pending-I/O/admission-balance claim beyond checked protocol cleanup,
and no inferred speedup from different source bodies. [Plan](plan.md),
[attribution](current-attribution.json), [raw/source/gates](observations.tgz),
[validation](validation.json), [logs](validation-logs.tgz),
[independent Windows reference](windows-reference-validation.json).
Raw SHA256 `6fdbb7437d03348945c8f15b0bb72cd8f85dcedd094edc843d2de8703fd56c59`.
