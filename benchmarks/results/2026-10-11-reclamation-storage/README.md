# Admission-free reclamation sweeps

Reclamation sweeps no longer allocate or request additional execution memory.
One logical collection replaces young/old aging, funded promotion, sweep-depth
tracking and related relief retries. The production change removes a net **78
lines**. Actual weak owners, allocation preflights, registration rollback,
conservative live-memory accounting and retained backing capacity remain.

This is a bounded part of protection simplification. Immutable scalar storage
and async startup-thread lifetime were simplified in preceding rounds. Mutable
scalar identity maps, forced exhaustion GC and native stack telemetry remain
pending. This round establishes no general speedup or elimination of process-wide
GC pauses; sort retains a small measured tradeoff and retained-record results vary.

## Storage and admission

The collection scans recent appends promptly and at most 4,096 retained entries
per ordinary sweep. A full sweep visits all entries. Collected owners are removed
in place; keeping live entries tracked requires no promotion or new admission.
Registration still reserves its entry fee and backing growth before publication,
revalidates after relief and refunds failed transactions without disturbing
another reservation. Escaped aliases retain their actual payload owners.

Backing pages contain at most 8,192 entries. The first page keeps ordinary small
List growth, and the page registry appears only when needed. Page and registry
capacity are accounted and retained until pool abandonment. Metadata estimates
and tiny-budget packing can change; scalar fees are unchanged in this round.
The contract is in [SPEC.md](../../../SPEC.md#146-memory-budgets).

The baseline denies a sweep of one already-funded live entry at a saturated
305-byte budget: promotion requires another 32 bytes. Both full and ordinary
candidate sweeps succeed at that same cap, preserve a separate 17-byte
reservation, and allocate zero managed bytes across 32 warmed sweeps. Additional
controls cover 17,000 retained entries at a saturated budget, small backing pages,
prompt recent reclamation behind retained populations, refunds across page
boundaries, and atomic denial/retry of simultaneous page/registry growth.

The original retained-slice fixture caught MemoryError, kept all slices, then
allocated a fresh 192-byte result. Changed metadata packing leaves only 108 bytes
free. Isolated diagnostics reconcile all 3,145,620 bytes as live entries and
backing, with zero dead charges or reservations; extra GC does not help. The
fixture now funds its result before saturation, verifies genuine retained denial,
clears retained storage and completes another 10,000-slice stream under the same
3 MiB cap, while one escaped slice stays usable. Catching MemoryError does not
create space for new containers. Original source/failure evidence is retained.

## Rejected single-array candidate

Initial candidate `2951bed102fa834e88ef3dca998eaa2c9df96963` passes 9,930 Debug
checks but makes stable sort 53.4% / 15.5% slower in two short replicas, with
29.0% / 54.2% candidate IQR/median. It was not integrated. One contiguous
tracking array grows to 16,384 references and lands on the large object heap.
Native captures show large ReclamationEntry[] allocation ticks and
allocation-triggered background generation-2 starts in that candidate; the
baseline capture shows small arrays. Weak-handle ordering/sorting leaf samples
rise from 6.9% to 15.9%. These observations support a GC mechanism, not a precise
causal CPU estimate or complete GC-count comparison.

The paged refinement preserves the single logical collection and admission-free
sweep. Its selected trace records only small reclamation arrays and no background
generation-2 starts. Baseline / initial / paged captures contain 1,357 / 2,705 /
1,370 CPU samples, with zero lost samples or missing stacks; each owned group
including cleanup takes 15 seconds. GC event pairs are incomplete, so complete
collection counts and pause durations are deliberately not reported.

## Producers and verification

Baseline: `804216afd07a95a146eb11865c1e206c66b0bc5f`, production tree
`8a7da46048c76d77e6e641242a65738e245f8676`.
Paged candidate: `c362b2436d14d38999d55357a1a1a734508929dc`, production tree
`99071b61a21b34753099117d0ef0c5bcf1ef0e64`, test tree
`6a0d4561cd6958d66966b702288e46f9c54f112e`.
The unchanged comparison harness is `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

All **9,936 Debug checks** pass: 1,903 white-box and 8,033 public, with an explicit
matching Probe build. Isolated VM Release checks pass 159 focused tests for the
paged candidate and 148 for the baseline. Four pure probes agree with CPython
in each execution mode. All five timing sources verify on both producers before
collection. Prior failed/regressed producers, receipts and traces are preserved
separately. Source, binary, helper, receipt and trace hashes are recorded in
[evidence.json](evidence.json).

## Short execution diagnostics

Pinned SDK 10.0.401 / CLR 10.0.12 and isolated release CPython 3.13.16 run on the
dedicated Ubuntu VM. Ordinary worker limits, tiering and GC remain enabled; only
the controller disables tiering. Compile/transport/hash preparation is outside
timing; ordinary execution and output checks remain inside. No VM builds, tests
or transfers overlap collection. Each engine gets one second of warmup,
approximately 25 ms calibration batches and seven rotating-order rounds.

The retained-record diagnostic constructs 2,048 actual `(string, integer)`
records, retains them through four sum loops and checks length/sum. The other
four sources are unchanged canonical controls, including 128-row CSV discard
and full stable-sort/output. No contexts, receivers or state are simulated.

Cells show median **microseconds/invocation**, followed by **IQR/median**.

| Case / replica | Baseline | Paged | CPython |
|---|---:|---:|---:|
| Empty / 1 | 23.605 (6.9%) | 23.468 (2.5%) | 1.465 (0.4%) |
| Integer loop / 1 | 1113.437 (0.4%) | 1130.297 (1.1%) | 729.264 (0.1%) |
| CSV discard / 1 | 533.957 (9.0%) | 535.015 (7.8%) | 167.327 (0.9%) |
| Retained records / 1 | 4361.761 (9.9%) | 5803.573 (35.6%) | 398.307 (0.5%) |
| Stable sort / 1 | 2712.759 (6.8%) | 2741.125 (6.1%) | 301.091 (1.1%) |
| Empty / 2 | 24.089 (3.6%) | 23.169 (4.5%) | 1.515 (0.9%) |
| Integer loop / 2 | 1106.219 (0.7%) | 1099.605 (1.0%) | 596.839 (0.6%) |
| CSV discard / 2 | 544.272 (1.9%) | 542.177 (10.7%) | 159.317 (0.2%) |
| Retained records / 2 | 5838.606 (30.4%) | 4791.678 (7.6%) | 397.741 (0.1%) |
| Stable sort / 2 | 2553.530 (5.5%) | 2736.883 (8.4%) | 303.030 (0.5%) |

Sort rises 1.0% / 7.2%; the remaining possible cost is accepted for simpler,
admission-free sweeping, without a speedup claim. CSV changes +0.2% / -0.4% and
integer-loop +1.5% / -0.6%. Retained-record medians flip +33.1% / -17.9%, with
7.6-35.6% spreads: no reliable direction is inferred. Both replicas are retained;
there is no recollection to chase a favorable median. CPython is context, not
proof of a newly qualified cross-runtime ratio.

The initial ten groups take 6.41-7.30 seconds and the final ten 6.46-7.14 seconds,
with 7-8-second cleanup totals. Actual protocol attempts are 418 / 419, including
210 measured batches per assessment. Every owned measured/diagnostic group stays
within 30 seconds including cleanup; terminal PID, empty control group and lease
reacquisition are checked. No full ten-minute lane or timing threshold is added.
