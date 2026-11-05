# Contextual nested hashes and shared managed admission — retained with debt

2026-11-05, D64E1/E2 source pair against `f2b7b8a7`, frozen `8050cc0f`.
Final frozen core `793586f5`, built before commit metadata; all13 changed source/
test/documentation files [verified](validation.json). Full4168internal/16033public
Release checks pass per actual.NET11/10. Platform/original Stress/installed-consumer
qualification is separate. This does not close E3 storage separation or the
whole-script performance objective. Publication remains postponed.

Slice bounds now use contextual Python hashing, including bound/unbound hash
methods, tuple/container/cache keys and start/stop/step order. Native hash
combination stays unchanged. Generated dataclass methods prepare their defining
field names under the existing class-plan admission, then reserve one temporary
values array before reading any fields. All reads precede field hashes; no tuple
wrapper is constructed. A base method no longer substitutes the subtype's plan.
Explicit `object.__hash__` retains identity behavior despite a disabled own slot.

Actual descriptor/hash callbacks check cancellation and first refused admission
before continuing. Native callbacks that swallow refusal cannot return a hash
successfully; explicit guest-raised MemoryError remains catchable. Snapshot funding
lasts through its users and unwinds deterministically, without collection.

Managed pair/single depth and stack checks share one policy, verified identical
except their existing comparison-message suffix before consolidation. Classifiers
borrow the existing depth-only Scope without identity-set registration or new
fields/options; actual hashing keeps Single cycle guards. The512-level bound,
configured recursion limits and managed stack checks remain. Tests cover deep/
cyclic classification, recovery and native slice hashing after real host suspension.

## Controlled measurements

Original B29986.16s/three jobs; one justified B30059.23s/two-job refinement.
Unchanged workerb94/protocol2, independent identical-current control and
CPython3.13.16, normalGC/tiering; five-second warmup per role/job and seven rotated
~150ms batches. Standard_F4as_v7/Ubuntu24.04.4 x64/AMD9V45x4/.NET11RC3.
All source/fixture/library/fullgolden/quiet/Closed/finalzero/worker-join/fixture-
removal gates pass. Native workloads have correct baseline semantics; newly
required guest callbacks are not compared against the incorrect skipping baseline.

Warm clocks include invocation, actual mediated RAM-file I/O, full stdout/file
validation and prior-result disposal. Compilation, startup, staging/reset,
transport and final program/result/process/fixture cleanup are excluded. Synthetic
grouping has no guest file I/O. No allocation, causal CPU, RSS, embedding-latency,
fresh-execution or compilation claim.

| Block/workload | Candidate/control ms | Reference ms | CPython ms | Current/reference elapsed | Status |
| --- | ---: | ---: | ---: | ---: | --- |
| Original complete CSV,4096records |4.882/4.904|4.926|3.360|−0.89/−0.43%|Small/variable; referenceIQR3.74%|
| Original complete text,3072lines |2.222/2.242|2.319|1.162|−4.20/−3.32%|Diagnostic; current spread0.92%|
| Original native compound grouping |10.715/10.650|9.903|0.924|+8.19/+7.54%|Adverse; current spread0.60%|
| Refined complete text |2.239/2.256|2.221|1.204|+0.83/+1.60%|Possible small debt; conflicts with original block|
| Refined native compound grouping |10.066/10.126|9.862|0.916|+2.07/+2.68%|Residual debt; spread0.60%, roleIQR0.15/0.91%|

`Current/reference=(elapsed/reference−1)×100`; negative means faster. Grouping is a
synthetic2048-record/eight-wrapper native-tuple control, not a representative file
script. The unchanged text reference shifted~4.3% between blocks; the original
text improvement is not robust and cannot be attributed to this change.

Retain contextual correctness and the shared managed policy with **native grouping
debt+2.07/+2.68%** and possible small text debt explicit. Original/adverse conditions
stay; no further retiming campaign selected here. Earlier B1/B2 synthetic custom-set
debt remains revision-specific and was not remeasured after E1/E2. Percentages are
not additive; there is no general CPU/performance gain claim.

[Original observations/source](original/observations.tgz),
[original medians/dispersion](original/summary.json),
[refined observations/source](refined/observations.tgz),
[refined medians/dispersion](refined/summary.json),
[refinement rationale before code](classification-refinement.md).
Raw SHA256: original `fe01d1a12fdd2d17125b54602ab0eb463fff308a20748015382918c4e67c4f95`,
refined `53c2edf8d32fdf936596f7e1167ae4a54d522183eb50f88959a6a7bd30690a8a`.

## Correctness and remaining contract

Focused1823public/502internal per actual target pass. Nine isolatedCPython3.13.2
goldens cover ordering, inherited methods, hash inclusion, errors, native
consistency and explicit root hashes. [Correctness history](correctness-history.tgz)
retains original probes, the initial missing unbound-member failure and incorrect
depth256 expectation (the actual bound is512), subsequent missing table registration
and all corrected results. Native swallowed-refusal/cancellation and preflight-before-
attribute-read regressions preserve origins, stopped effects, funding and unwind.

E3 remains demonstrated and unresolved: structural dataclass storage can repeat
attribute reads or reject a valid custom hash with structurally unhashable fields.
Its field-hash loop has real callers, so it remains rather than being deleted or
wrapped prematurely. Identity-only storage requires migrating semantic Counter,
mapping-pattern and view callers and checking compound public hash semantics;
blindly changing the global comparer would regress supported behavior. This is
explicitly separate from corrected public/contextual hashing. Hash callbacks still
require synchronous completion; genuine async paths elsewhere are preserved.
