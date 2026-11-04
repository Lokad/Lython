# Executable storage and native-source consolidation B207

2026-11-04. Runtime commits cee1f4a6,238ec0b7,e2653ea5,05bbf29f,2e0f14d9,0ffc9ac6;
delivery qualification is pending. Original frozen core dfd4b5da; final readonly
refinement adb70567. Qualified baseline bf0d38f3/frozen454cd98c. Full hashes and
source snapshots are in the identities/archives. None is a CPython comparison.

## Result and limits

The existing executable engine now uses compiler-prepared storage positions,
one invocation-owned array for locals and temporaries, and instruction/call
metadata for native-return provenance. Remove Push/Pop/Peek transport, dynamic
shape helpers, the separate NativeReturnNode array/list and duplicate local-array
allocation/funding. Retain actual live extents, original failable reads, evaluated
call packets, cells, pending disposals and deterministic ownership through real
suspension. No new backend, opcode, policy, pool or public option.

Actual instruction stride24->40B; syntax/executable argument metadata remain16B.
Ordinary workspace capacity stays conservative, rather than growing with code
length. Native helpers require no new packet copies. Small executable bindings
use fresh combined arrays instead of tiny bound pools. These are tradeoffs,
not an allocation or CPU improvement claim.

Warm whole-script allocation is effectively flat (-0.010%..+0.025% across the
eight existing cases, both APIs). Compilation costs more in the initial source
pair: text+2.4..5.5%, report+1.85..1.94%; text controls differ3.0%. This debt
remains visible. New readonly borrowing changes runtime helpers, not that compiler
source; these compilation measurements still identify their original frozen bits.

## Initial complete scripts: original prototype, not the final refinement

Twelve jobs compare qualified454 before / dfd4 prototype / qualified454 after.
Below, signed elapsed change is prototype/baseline-1; negative means faster.
Ranges use the two independently warmed identical-source baseline processes.

| Workload | Sync change | Async-host change | Interpretation |
| --- | ---: | ---: | --- |
| Text summary/helpers | -0.6..+1.3% | -3.6..-1.8% | Sync control-limited |
| CSV summary/helpers | -3.3..-0.9% | -2.2..+0.3% | Small/control-limited |
| JSON-lines/helpers | +0.4..+3.0% | -0.8..-0.6% | Sync controls differ2.6% |
| JSON-to-Markdown report | -3.1..-0.3% | **+7.84..7.89%** | Async adverse, old spread0.04%, candidateIQR2.79% |
| Branching ordinary-call control | -7.4..-5.0% | -4.0..+3.6% | Async controls differ7.3%; control allocation unmeasured |

The ninth ordinary-call diagnostic triggered the existing allocation driver's
fixed-eight-case guard, after all twelve timed workers had joined. Thus the
original collection has no ending machine gate and its complete flag stays false.
Only allocation was resumed with the unchanged eight-case catalog: goldens,
balances, worker joins and separate quiet gates pass. No original timed samples
were replayed. The [initial archive](initial-observations.tgz) preserves the failed
stderr, original timing JSON and correction receipt. [Timing summary](initial-timing-summary.json),
[allocation summary](allocation-summary.json).

## Bounded investigation and correction

One report async-host CPU profile used existing perf cpu-clock199Hz/frame-pointer
collection: old/prototype1834/1857 guest samples and9.23/9.34 guestCPU seconds.
Native evaluator leaves rise3.0->5.8%; JSON contributions vary. This suggests a
transport cost to investigate, without explaining the entire8% elapsed result.
The first attempt collected no samples after a control timeout on a full disk;
failed logs/zero traces remain. Verified backup/removal of unused package-test
staging recovered523MB. The corrected profile passes gates/goldens/joins.
[Original and corrected profile evidence](profile-observations.tgz).

The coherent correction borrows immutable instructions by managed readonly
reference throughout native guards, evaluation, formatting and operand reading.
It removes value copies of the40B struct without extending any suspension lifetime.

One four-image confirmation: qualified454 / unchanged dfd4 / refined adb7 /
qualified454,20s warmups and11 rotated300ms samples. Completes102.53s; all
identities/goldens/quiet gates/normal closes/joins pass.

| Image | Median report async-host | Versus baseline roles |
| --- | ---: | ---: |
| Baseline before |4.766ms|—|
| Unchanged prototype |4.960ms|+4.07..5.53%|
| Readonly refinement |4.675ms|-1.91..-0.54%|
| Baseline after |4.700ms|—|

Refinement is5.75% faster than the unchanged prototype in this capture. Baseline
spread1.38%, IQRs1.97..3.39%: final performance is close to baseline, without an
established broad CPU gain. Original+7.8% and the smaller confirmed prototype cost
remain. [Confirmation summary](refinement-summary.json), [raw evidence](refinement-observations.tgz).

## Conditions and correctness

Existing quiet AzureF4as_v7/Ubuntu24.04.4, actual.NET11RC3, normal workerGC/tiering.
Warm execution includes mediated RAM-file I/O, full output/file checks and
prior-result cleanup. Startup, fixture staging/reset, response hashing and final
cached teardown are excluded; compile-only includes compilation/disposal separately.
Initial jobs use6s warmups/seven rotating150ms samples. Isolated allocation uses
the existing eight-case driver. Profiles use PerfMapEnabled only for symbolization;
profile elapsed is not an uninstrumented timing confirmation.

Focused Release1110 internal/3527 public per actual11/10 pass after shared storage;
direct-input stage1121/3527, plus routing/unbound/scope/cleanup coverage. Native
borrowing505/2750 per target passes. Nonincremental benchmark rebuild has zero
warnings/errors and matching core/test/worker hashes. Original15/14 buffer-fixture
failures, one opcode-choreography fixture and five missing-probe-build failures
are preserved privately; corrected tests keep independent ownership/cleanup assertions.
Full platform, original stress and installed consumers belong to the delivery boundary.

Before delivery,0ffc9ac6 moves compaction onto owned blocks before linearization.
Delete the global compaction map, extra full instruction copy and duplicate
branch/exception remapping (36 net frontend lines removed). Native eligibility
uses its existing straight-line requirement. Both runtimes produce identical
active instructions/spans/arguments/slots/protected ranges across17 cases,
38 code objects and8 native roots; only inactive partial proof fields are ignored.
Consolidated focused1121 internal/3527 public per target passes, with matching
nonincremental core/test/benchmark hashes. [Source gate](compiler-source-gate.json),
[complete metadata and read-only diagnostic](compiler-source-gate.tgz).
The later CPython checkpoint will measure this source's compilation separately;
the initial compiler costs above are not silently withdrawn.

Whole-script performance remains open. Earlier CPython ratios describe their
recorded source; do not add these source-pair deltas to them.
