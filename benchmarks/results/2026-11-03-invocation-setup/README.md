# Invocation setup gate and namespace correction — B148

2026-11-03, source713aab43 plus the retained namespace correction identified in
[identity.json](identity.json). This is a component diagnostic, not a before/after
runtime speedup or a new CPython comparison. Existing public runtime, benchmark
host and compilation paths are reused; only the private builtin-table constructor
is invoked through a prepared delegate. No source instrumentation, forced GC or
tiering overrides. Quiet VM gates pass; both owned services/processes joined,
funding returns to zero and the shared lease is free.

| Component | Initial medians, µs | Larger-batch medians, µs | Allocation/invocation |
| --- | ---: | ---: | ---: |
| Builtin table only |3.12 /2.79|2.71 /3.47|13,144B|
| Empty synchronous execution + result cleanup |21.76 /21.67|21.24 /20.38|16,280B|
| Empty asynchronous execution + result cleanup |14.21 /15.46|17.30 /18.89|about16,600B|
| Empty compilation + disposal |3.09 /2.71|1.85 /1.85|about8,296B after warmup|

Two independent processes per condition; nine alternating-order samples each.
Initial component batches were too short. The bounded larger-batch correction
retains those original observations and source; it does not resolve natural
GC/tiering variability. IQR reaches about30–67% for builtin construction, and
execution samples also drift. **Exact CPU cost and sync/async ranking remain
unresolved.** Stable allocation reduction potential is distinct from CPU evidence.
Do not compare these component medians as if they were causal speedups.

Builtin construction is observable fixed work, but this gate does not establish
it as a dominant cause of the representative workloads taking hundreds or
thousands of microseconds. Broad builtin/type sharing is deprioritized pending
stronger evidence, not prohibited. The existing type graph also retains lazy
execution-owned namespace backing, so static sharing is not licensed by this test.
The performance roadmap continues with coherent UTF-8 fragment storage.

The retained correctness change returns a fresh class mapping proxy for every
`__dict__`/`vars` access, matching the [independent CPython golden](cpython-namespace-golden.txt).
One cached dictionary retains shared labels and structural mutation tracking;
instance dictionary identity is preserved. Each proxy reports its actual admitted
owner/span. Repeated proxies stay conservatively charged until execution/result
cleanup. This is a semantic correction, with no CPU improvement claim.

Focused Release validation on actual .NET11/.NET10:13 namespace public cases,
4 namespace admission cases and160 related public cases each PASS. Coverage includes
live views, replacement/add/delete invalidation rules, retained result projections,
refusal without publication, terminal refusal, cancellation and shared-budget reuse.
Full Windows/Ubuntu.NET11/.NET10, original Stress and installed-consumer checks
PASS on exactd7b715d3, [cached run41583008474](delivery-ci-status.json). This qualifies
the retained namespace correction, not the later withdrawn storage prototypes.

Raw data: [initial first](setup-first.json), [initial confirmation](setup-confirm.json),
[larger first](setup-large-first.json), [larger confirmation](setup-large-confirm.json),
[derived larger summary](setup-large-assessment.json). Exact operation boundaries,
whole-process allocation observations, core hash and runtime identity are in each
raw file. [Source and focused logs](source-and-focused-logs.tar.gz) preserve both
batch sizes, hardcoded development paths and original commands. They describe
the recorded source and environment, not future HEAD.
