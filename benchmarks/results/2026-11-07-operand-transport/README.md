# Ordinary-operation operand transport — R43

2026-11-07. Retained compiler/runtime consolidation; no substantial CPU gain
established. Baseline production `fba865ef`, frozen `5d2ca8e1`/`2aefaa39`.
Measured final prototype `8b5ce5bc`/`ed80f283` (.NET 11/10), precommit metadata
`58d14512`. Exact images/sources and intermediate variants are archived.
The primary CPython table remains the separately qualified B395 baseline.

## Representation and deletion

Extend existing integer constant/local/temporary sources. Adjacent uncaptured,
nonmirrored plain local/constant reads feed ordinary binary/unary/subscript
operations and stores/returns without separate load instructions. Missing reads
keep their original source spans in sparse cold metadata. Forward before the
single physical-layout pass; remove initialized-local tracking/invalidation
inventories and repeated value/store selectors/helpers. Native helpers, closures,
dynamic lookup, evaluation order and genuine protocol snapshots remain.

The initial third-input field grows instructions 32→40 bytes. Refine this by
reusing a subscript store's redundant result-position field; its result position
equals its live extent. Every final instruction remains 32 bytes on the measured
.NET 11 runtime, including unchanged native helper bodies. Remove the prototype's
frame-wide snapshot cache/index, cleanup hooks and 64-byte reservation. Context
promotion runs no guest/admission callback; type getters reuse existing funded
protocol slots. Async operations capture actual values until completion. No new
memory layer, arena, allowance, policy, public option or alternative engine.

| Code | Instructions before / after | Instruction + cold-read-span payload before / after |
| --- | ---: | ---: |
| Text module | 85 / 77 | 2,720 / 2,592 B |
| CSV module + helper | 112 / 100 | 3,584 / 3,376 B |
| JSON module + helper | 100 / 91 | 3,200 / 3,040 B |
| Report module + helper | 84 / 75 | 2,688 / 2,496 B |

These are static payload counts, not whole-program retained memory or CPU shares.
Workspace capacities remain unchanged: calls still determine their maxima.

## Allocation and original adverse evidence

Reused complete-job driver, normal GC/tiering, isolated images sharing process
GC/CoreLib. Initial warm allocations are essentially flat (−0.021% to +0.003%).
Desktop elapsed observations conflict: text sync −15.75% accompanies an identical
baseline −27.63% control; async text is +20.88%. No desktop CPU claim follows.
Initial compilation allocation is +2.8–3.5%. After metadata/cache simplification,
the separate compilation diagnostic is +0.20–0.87%; all output/file goldens,
ownership balances, image isolation and fixture cleanup pass. Final warm allocation
was not separately replayed. Smaller code payload does not prove lower compilation
allocation, live heap, RSS or embedding latency.

## Controlled complete-script comparison

Quiet Azure Standard_F4as_v7, Ubuntu 24.04.4, .NET 11 RC3, unchanged worker
`7cf37ca5`/adapter `27211dbe`, normal optional PythonRe 0.4. Two baseline/two
current processes; five-second warmup per role/job, seven rotated ~150 ms rounds.
Commands include RAM host I/O, full aggregation/goldens and prior-result cleanup.
Compile-to-result adds compilation/program disposal. Startup, staging/reset,
transport and final cached teardown are excluded. This is not a CPython refresh.

B397 completes warm text, then rejects my unsupported `fresh` lane. The worker
expects `compile-run`; this is a collection setup error. Its partial observations,
stderr, original source, archive-helper syntax error and terminal cleanup remain.
No complete performance conclusion follows from that partial collection.

B398 corrects only the protocol lane with identical images/source. All five jobs
complete in **141.38 seconds**, below the 180 s owner +30 s stop +30 s margin.
Quiet/image/golden/funding/Closed/exit/join/fixture/archive gates pass. Negative
change means faster; ranges include every current/baseline process-median pairing.

| Complete job | Change | Identical current / baseline spread | Interpretation |
| --- | ---: | ---: | --- |
| Text warm | −1.37 to +1.11% | 2.23% / 0.22% | Variable |
| Text compile-to-result | −2.23 to −0.15% | 1.43% / 0.67% | Small favorable, control-limited |
| CSV warm | −1.57 to +0.76% | 0.16% / 2.20% | Variable |
| JSON warm | −2.37 to +6.81% | 6.66% / 2.52% | Inconclusive; adverse process retained |
| Report warm | −2.19 to −0.73% | 0.80% / 0.68% | Small favorable; IQR up to 3.92% |

Coarse process CPU is separate and does not establish a CPU improvement. Retain
the consolidation, fewer instructions and smaller immutable payload; accept small
compiler-allocation cost, variable elapsed and unresolved JSON process variation.
No unchanged retiming is selected. Earlier debt and the substantial whole-script
performance objective remain open; next assess duplicated completed-instruction
checks rather than continuing leaf-specific experiments.

## Correctness and delivery status

Initial and metadata-refined prototypes each pass 1,167 internal / 2,665 public
focused Release checks per actual target. Removing the frame cache then passes
128 internal / 676 public affected checks per target. New three-input stores
preserve aliases through rebinding/suspension, cancellation and first refusal;
following mutation stops, cleanup finishes and shared funding/depths recover.
Unbound reads retain their original name/span and effect order. Existing class
subscription checks cover descriptor/body suspension and retained results.

After freezing, remove dangling prototype comments and restore the original checked
workspace multiplication; that file exactly matches the qualified baseline again.
Delivery images `9eb7a6b5`/`7fe742dc` pass nonincremental builds and 70 internal /
47 public affected checks per actual target. This source cleanup is explicit;
there is no binary-equivalence claim with the timed images. Full Windows/Ubuntu,
Stress and installed-package delivery is pending. No public API break, adapter
promotion or publication.

[Summary](comparison-summary.json), [identity](identity.json),
[initial allocation/adverse clocks](initial-local-summary.json),
[final census/compilation](final-local-summary.json),
[original failed collection](b397/failed-observations.tgz),
[corrected raw/source/gates](b398/observations.tgz),
[development sources/images/checks](development.tar.xz).
B397/B398 are terminal, archived and locally hash-verified; never service-query
again. Both have no remaining workers/guest fixtures. Archives preserve original
hashes and conditions; development images are diagnostic evidence, not packages.
