# Annotation initialization from lexical facts

Runtime `84062140` records variable-annotation presence during the existing
lexical-binding pass. Dead branches and non-name targets count; nested function
and class bodies are excluded from their enclosing scope. Both hosting APIs,
class setup and imports reuse this fact and the existing first-statement span.

The separate recursive `ContainsAnnotation` scan, per-run statement-tree access
and compiled wrapper's `ScriptSyntax` property disappear. No extra compiler pass,
cache, analyzer, memory layer or public option. Initialization still creates its
dictionary at execution time under the same owner and preflight. Supplied
namespaces/aliases, hooks, error attribution and cleanup remain. The coarse
compilation charge is unchanged until its owner ends. Real class, annotation
and lowered opcode consumers retain their needed data; this is not a claim
that all syntax or physical memory is reclaimed.

Focused Release passes: 98 public annotation/import checks, 40 white-box binding/
ownership/unwind checks and 149 complete representative scripts, alias and file
retention checks. Selections overlap. New small controls cover setup before the
first statement, dead loops/handlers, lexical exclusions and supplied annotation
namespace aliases. Existing controls cover metadata suspension/cancellation,
admission and retained namespaces. No assertion, timeout or allowance was relaxed.
[Receipts](checks.json) and [12 independent CPython 3.13.2 goldens](oracles.json)
are preserved. CPython executes in a fresh namespace with `__name__='__main__'`,
matching embedding; its CLI pre-seeded annotation dictionary would invalidate
absence controls.

One new control identified a pre-existing compatibility gap: ordinary nongeneric
class annotations expose legacy metadata markers, so `Box.__annotations__['item']
is int` differs from CPython. The frozen `a017d12a` [public probe](class-probe.json)
and [loaded-library identity](class-probe-library.json) establish that this is
unrelated to initialization migration. The final lexical-scope control compares
annotation keys. Evaluation/identity is recorded as a separate correctness task
and documented in SPEC; this change preserves the existing class paths. Two
additional [frozen witnesses](class-gap-probes.json) confirm skipped eager
callbacks and conditional class setup incorrectly reading module annotations.

One declared warm diagnostic reuses the 2,000 assignment declarations generated
by `CompileOnceRunManyTests.LargeSource`; only its final top-level return becomes
`print` so both engines execute identical ordinary Python and produce `3998`.
The module is compiled once per worker, then recreated/executed per invocation.
This isolates the affected initialization consumer rather than compilation cost.

| Engine | Warm complete module | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `0ed30179` | 0.193 ms | 7.72% | Before; diagnostic |
| Lython `84062140` | 0.155 ms | 18.04% | After; diagnostic |
| CPython 3.13.2 | 0.045 ms | 4.37% | External baseline; diagnostic |

The nominal Lython median is 19.7% lower in this one capture. It remains
unqualified, with substantial candidate spread; no general speedup, allocation
reduction or embedding-latency claim. The after/reference ratio is about 3.41
(Lython elapsed / CPython elapsed). No retiming or full lane.

Measured 2026-10-20 UTC on shared LOKAD-0399, Windows build 26200, x64/28 logical
processors, .NET 10.0.13/workstation GC and ordinary CPython GIL/GC. Module/context
construction, execution/result cleanup and exact output checks are included;
guest compilation, startup and transport are excluded. The timing root is
explicitly unbounded. Seven alternating rounds follow warmup. Parent and workers
finish in 7.544 seconds without forced GC. [Declaration](declaration.json),
[frozen before identity](baseline.json), [catalog](catalog.json), [raw samples/
loaded identities](micro.json) and [process receipt](micro.receipt.json) retain
the boundary and evidence.

Full Windows/Ubuntu Release, original Stress and both explicit package consumers
pass at exact runtime revision `84062140` in [CI 39352958182](https://github.com/Lokad/Lython/actions/runs/39352958182).
[All required final steps](delivery.json) were inspected. Three spaced queries;
terminal green is cached forever and must never be queried again. Publication
remains deferred.
