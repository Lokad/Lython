# Wrapper construction and copy ownership

Counter/defaultdict construction now has one shell-admission path per wrapper.
The 64-byte shell stays reserved until its inner dictionary succeeds; child
denial refunds that reservation. Default factories are validated before
admission. Inherited-governor copies fund the shell they report. Copies clone
the inner dictionary once instead of creating an unregistered intermediate.

Fresh owners are registered before constructor input, union population and
deep-copy callbacks. Failure therefore remains reclaimable through the existing
ownership snapshots. A partial clone retained by a callback keeps its live
funding. ChainMap copies use the same single-clone construction. Caller-side
duplicate shell charges and dictionary copies disappear: **20 net production
lines removed** across six files. No new ownership class, identity map, tracking
tier, configuration or population-rollback transaction is introduced.

The original controls demonstrate four stranded 64-byte shell charges, two
384-byte copy snapshots with only 320 bytes admitted, and four bounded public
workloads failing after repeated invalid factories, failed population or dropped
copies. All ten accounting controls now pass with their original caps and counts.
Pinned committed/reserved storage and registration-denial controls pass too.

Counter accumulation reuses dictionary key-hashability validation, rejecting
unhashable list/dict/set/nested-tuple input. Numeric and Boolean equality/identity
remain unchanged. Custom hash/equality callback counts and asynchronous protocol
dispatch remain separate work: the observed callback behavior is not claimed
fixed here.

Metadata admission now occurs before input callbacks and may deny earlier at
tight budgets; inherited copies require their real shell headroom. Supported
results, source/copy aliases, callback-visible effects, cooperative cancellation,
allocation preflights, cycle handling and bounded projection are preserved.
MemoryGovernor, forced GC, execution threads and stack policy are unchanged.

Implementation: 9af982c3; public lifetime tests: d7b87c03; error-catcher refinement:
2c351cb1. Qualified production tree: 28ba4d503bf68f0d9f5c52b57477c9d45d5068b2.
The [evidence](evidence.json) pins actual Debug/Release DLLs and their build
producers, source trees, original faulty observations and terminal test receipts.

Validation passes **10,310 complete Debug tests** (2,100 white-box, 8,210 public)
and **116 focused Release checks** (36 white, 68 core public, 12 lifetime/hash
checks). Focused groups take 2.1–7.4 seconds including cleanup. New public cases
use genuine delayed-host I/O before bounded failure/copy workloads. Copy/union/
ChainMap turnover, escaped and dropped partial deep clones, failed population
and unhashable Counter input pass in both execution modes.

One initial broad public selector included long lifetime scenarios and was
terminated after 28.049 seconds; its partial output is not qualification.
Two initial lifetime checks caught only ValueError for malformed defaultdict
pairs, while the current runtime reports TypeError. The corrected catcher
matches the original TypeError/ValueError control without relaxing any input,
iteration count or memory cap. That Python error-category discrepancy remains
a separate compatibility item. Original harness/filter failures are retained
and excluded from accounting conclusions.

This is an ownership/correctness simplification. No performance collection or
comparison lane was run. Forced-GC independence, other construction/copy lifetime
paths, Counter protocol fidelity and broader runtime/benchmark work remain open.