# Small immutable tuple adoption

Tuple construction no longer builds temporary scalar identity-count and rollback
collections for tuples of up to two items. Two short replicas show **24.1% / 25.9%
less time** for the tuple-building job. Exact logical charges, alias handling,
tracked-scalar exclusion and denial rollback remain intact; the change is integrated.

Eight prospectively declared micros finish in **6.40–7.27 seconds**, with a
**30-second external process-group cap** each. Separate retained-construction
allocation diagnostics finish in about two seconds per owned group. No full lanes
run. These are diagnostic observations, without milestone qualification or certified
Lython/CPython multipliers. Complete sort results remain too noisy to claim a
reliable gain from this particular change.

## Design and source

The [current construction profile](../2026-10-10-integer-sort-keys/README.md) identifies
scalar adoption as a useful lead (20.10% inclusive sampled CPU, overlapping with
tuple evaluation and allocation). Governed tuples previously created an
`AdoptedScalarCoupons` helper with a reference-identity dictionary and staged list,
then discarded those collections and retained only the total charge. They are
needed for general mutable-container refcounts and arbitrary-size identity dedup;
small immutable tuples can dedup directly from their retained input slots.

Candidate `a9f8d93f30fa23540454a1cdb9a7d128b89eb76d` has production tree
`4f7d7c7ec49120a3df1354321536ec816f1c5e95` and test tree
`89ce075a75930a65f12a1bda4c89cf70f1357455`. The baseline is the accepted integer-key
sort build `afe6bbc48164a1c02efd6b357a5f27c6c2eb6b82`, production tree
`baee33a49691abc7af5623f5d9b61011741bdefc`, identical in production to then-delivered
`6dcf17b4662d386dbd8d3ac8ec8525a3cce1f4af`. Comparison-harness tree
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d` is unchanged.

The small path checks the existing scalar type gate and global tracked identity,
dedups a second slot only by reference identity, and keeps every 64-byte per-scalar
reserve/commit. If a later coupon denies, it refunds exactly the coupons already
committed; the caller refunds the unpublished tuple backing. No new owner is
published on denial. Larger tuples retain the general adoption path, and the owned
array factory uses the same helper as copied-array and enumerable construction.
Tuple backing estimates, 128-byte registration fees, governor limits, allocation
checkpoints and host mediation remain unchanged.

## Separate allocation diagnostic

The helper retains 512 warmup and 10,000 measured tuples per shape, all alive through
the end of the process. Each shape has its own governor/pool. Prebuilt boxed inputs
stay outside the counter, while construction and registration are measured with
`GC.GetAllocatedBytesForCurrentThread`. No forced GC. This is helper construction
and registration allocation, not public invocation allocation, native handle
storage, resident memory, peak memory or a CPython memory ratio.

| Retained construction shape | Before bytes/tuple | After bytes/tuple | Logical committed bytes, both |
|---|---:|---:|---:|
| none | 241.4352 | 177.4352 | 2,149,376 |
| distinct | 546.1232 | 178.1288 | 3,494,912 |
| alias | 545.5904 | 177.6512 | 2,822,144 |
| general3 | 553.6000 | 553.4072 | 3,663,104 |

`none` uses two non-scalars; `distinct` two distinct boxed integers; `alias` one box
twice; `general3` the alias plus a distinct scalar in a larger tuple. Distinct and
aliased pairs allocate about **67.4% fewer managed bytes** in this helper. The
non-scalar pair saves 26.5%. The three-item control differs by only 0.035%, showing
the general path is essentially unchanged in this diagnostic. All shapes register
10,512 tuples and preserve the exact same logical charge on both producers.
The small residual includes helper/runtime effects and is not an exact per-object
layout proof. Allocation savings alone do not decide acceptance.

## Short invocation timings

Both producers use pinned SDK 10.0.401 / CLR 10.0.12 on the dedicated Ubuntu VM;
context uses isolated release CPython 3.13.16. The previously frozen, prepared
baseline is revalidated and reused. Timed Lython workers retain ordinary settings,
governor/instruction defaults and GC; only the separate controller disables tiering.
No tracing, tests, builds or transfers run on the VM during timing. Full Debug
validation runs on the separate local machine.

Each engine gets one second of warmup, roughly 25 ms batch calibration and seven
rotating-order rounds. Identical precompiled source constructs fresh inputs per
invocation. The 2,048-tuple construction case includes range/reverse iteration,
modulo, comprehension, adoption and scalar length output; it is not isolated
constructor latency. Canonical sort calls the guest key once per element, stably
sorts and prints the entire list, checking every tuple against an independent
group-order golden. Cheap success and complete-output checks stay inside the clock;
compilation, transport and output hashing stay outside.
Full catalogs are byte-identical and all four jobs verify against CPython first.

Cells show median **µs/invocation**, followed by **IQR/median**. Both replicas are
retained, with replica 1 completing before replica 2.

| Case / replica | Before | After | CPython |
|---|---:|---:|---:|
| Empty / 1 | 23.021 (4.1%) | 23.124 (3.3%) | 1.473 (0.5%) |
| Integer loop / 1 | 1634.377 (0.6%) | 1639.659 (0.8%) | 597.344 (0.2%) |
| Build tuples + length / 1 | 1067.461 (6.6%) | 810.430 (7.1%) | 48.988 (0.1%) |
| Canonical full sort + output / 1 | 2889.311 (9.2%) | 2792.081 (8.4%) | 304.589 (0.4%) |
| Empty / 2 | 23.399 (4.0%) | 23.311 (6.9%) | 1.480 (1.3%) |
| Integer loop / 2 | 1644.903 (0.5%) | 1622.971 (1.4%) | 582.401 (0.2%) |
| Build tuples + length / 2 | 1080.606 (3.3%) | 800.956 (7.1%) | 48.930 (0.3%) |
| Canonical full sort + output / 2 | 3115.192 (10.0%) | 2666.418 (3.4%) | 303.441 (0.6%) |

The construction improvement repeats, with about 7.1% candidate spread in each
replica. Empty and loop controls have small mixed changes. Complete sort/output
medians improve 3.4% / 14.4%, but the first improvement is smaller than its spread;
these results do not establish a reliable sort gain. There is no material repeatable
guard regression. Acceptance rests on construction timing and correctness, with
allocation evidence corroborating the intended design change. Construction and
complete sort remain substantially slower than CPython in these diagnostics.

## Correctness, audit and cleanup

Twelve new white-box cases exercise the three construction forms across empty,
singleton, aliased/equal-but-distinct, non-scalar and three-item boundary inputs;
denial on the first or second coupon with exact refund/recovery; and already pooled
scalar owners. Existing coupon, reclamation, budget and lifetime contracts continue
to pass. Local focused white Debug/Release each pass **102 checks**, and selected
public Release passes **273** after a matching probe build. One initial local
command failed before launching tests because its output directory did not exist;
the directory was created and the single measured Debug test run has its own TRX.
No test outcome is inferred from that shell exit code.

The frozen VM candidate passes **1,399 white Release / 670 selected public Release**
checks and explicit CPython output verification after the matching Release probe
build. Timing is declared after those checks, while full Debug remains running
locally. Acceptance waits for its terminal result: all **9,152 Debug checks pass**
(1,399 white / 7,753 public), with the matching Debug probe and frozen binaries.
No failed test or timing attempt is retried.

Independent audit recomputes every median/IQR from seven rounds and checks all
**333 micro responses**, request IDs/counts, source/fixture/output hashes, pre/post
full outputs, loaded source versions, SDKs and worker defaults. Allocation helper
source/binary manifests, fixed counts, logical balances and producer hashes are
checked. Owned service journals prove both allocation groups started after their
declaration and completed/stopped within the 30-second bound.

Final VM audit rehashes both producers and the helper, confirms both source trees
clean, all **11 owned services terminal**, 26 recorded helper/micro PIDs absent,
and a fresh available shared lease. Raw declarations, receipts/TRX, helper source,
journals and cleanup proofs remain private under
`.git/agent-notes/small-tuple-adoption-20261010/`; maintained [evidence](evidence.json)
retains all rows and receipt hashes. Remaining expression/rendering costs, original
CSV/streaming failure causes and held namespace work remain separate investigations.
Full old/new and secondary-lane qualification await a declared consolidated milestone.
