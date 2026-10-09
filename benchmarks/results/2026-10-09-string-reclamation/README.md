# String reclamation improvement, 2026-10-09

Short experiments support keeping string reclamation registrations directly on
their owners. The complete ASCII pipeline's median fell from **1.32–1.34 ms** to
**0.43–0.47 ms** in two predetermined repetitions. This is diagnostic evidence;
no full lane was collected and no milestone ratio is certified here.

The previous [milestone](../2026-10-09-first-milestone/README.md) identified the
ASCII pipeline as a qualified slow job. Five decompositions and a separate
15-second CPU trace were declared before this production experiment.

## Measurement boundary

The unchanged pipeline replaces `::` with `/`, splits on `/`, joins with `|`,
and prints 10,241 UTF-8 bytes including the newline. Its literal fixture is
`abZ!::tail/` repeated 1,024 times. It contains no loop or casing operation.

Both engines compile the same ordinary Python source before warm measurement,
then execute each complete job with fresh state. Complete output is validated
on every invocation. Lython uses its synchronous public API with the ordinary
containment, dedicated execution stack, memory limits and output/result
projection. CPython uses ordinary isolated execution and capture, without an
equivalent in-process governor. No entry cost is subtracted.

All timed workers use normal tiering/PGO and GC settings. The supervisor alone
disables its own tiering; worker receipts confirm removal of that override.
Builds, transfers, tests and CPU profiling occur outside timing collection.

## Decomposition and trace

The baseline is `4fb489d97783aab6cadc94387661f60378f689c5`; its production tree
also matches `b231e7b9`. Each initial diagnostic deliberately launches two copies
of that same runtime, plus CPython. These are absolute observations, not an
old/new comparison. Medians below are microseconds per complete invocation.

| Job | Lython copy 1 | Lython copy 2 | CPython |
| --- | ---: | ---: | ---: |
| Original pipeline and full output | 1,355.49 | 1,268.03 | 45.22 |
| Pipeline and length output | 1,163.58 | 1,241.59 | 41.15 |
| Replace and length output | 103.51 | 103.26 | 11.18 |
| Split pre-replaced literal and count output | 1,073.60 | 1,001.43 | 23.54 |
| Print precomputed literal only | 85.62 | 84.41 | 2.59 |

Split/count eagerly constructs the complete list. It validates the scalar
output; it does not separately print or compare every intermediate string.
The original pipeline's full output and the correctness suites provide broader
semantic checks. These jobs differ, so their timings must not be subtracted or
assumed additive. Split was the investigation lead, not a proven attribution
of the whole gap.

The unchanged pipeline's separate on-CPU trace contains **10,004 samples**, no
lost events or missing stacks. Split appears on 40.35% of sampled stacks and
its ownership registration on 22.57%; the encompassing split callable appears
on 62.92%. Native leaf symbols are unresolved for **94.43%** of samples, so this
is directional managed-caller evidence, not precise native attribution.
Inclusive shares overlap and must not be added. The trace is not used for timing.
Its service finished successfully in 20.30 seconds under a 30-second cap.

## Accepted experiment

Candidate `897cbfdb3db4f8907409d00ed62ca89b64e00e3b` lets a string hold its
reclamation entry directly, avoiding an additional process-wide weak-table
entry and handle per string. The pool still refers weakly to the string, so a
registration cannot keep a dropped value alive. Other value types retain the
table path. Identity deduplication, cache-charge updates, transactional denial,
refunds, sweep cadence and conservative registry charges remain in effect.
The added reference fits within the existing conservative string estimate.

Two repetitions each of the original pipeline and split/count were declared
before collection. Each collection took **7.17–7.46 seconds**, including worker
startup, warmup, verification and shutdown, with a 30-second systemd cap on the
whole owned process group. All services succeeded, recorded workers exited,
the lease became free and all recorded inputs rehashed unchanged.

| Job / repetition | Baseline µs | Candidate µs | CPython µs | Baseline / candidate IQR fraction |
| --- | ---: | ---: | ---: | ---: |
| Pipeline / 1 | 1,316.97 | 430.42 | 42.00 | 0.226 / 0.013 |
| Pipeline / 2 | 1,343.06 | 468.32 | 45.58 | 0.402 / 0.054 |
| Split/count / 1 | 1,125.85 | 387.42 | 25.54 | 0.246 / 0.066 |
| Split/count / 2 | 974.18 | 363.22 | 23.39 | 0.265 / 0.030 |

Candidate medians are consistently lower: 65–67% for the complete pipeline,
63–66% for split/count. The old runtime is noisy and these seconds-long runs
do not meet milestone qualification. The exact source change is isolated, but
the experiment does not identify every indirect allocator/GC effect. A material
CPython gap remains; a full milestone is deferred.

## Reproduction and evidence

Use the Release comparison executable to emit `--compare list --profile quick
--out quick-catalog.json`, then run
[`create-diagnostic-catalog.py`](create-diagnostic-catalog.py) with that catalog
and a fresh output filename. This recreates the literal sources and explicitly
written independent goldens used here. Only trusted benchmark sources should
be passed to the CPython adapter. The diagnostic catalog belongs to `micro`;
it does not alter the canonical milestone profile or its qualification rules.

Run `--compare micro --catalog <diagnostics.json> --dotnet <pinned-dotnet>
--baseline-worker <old-release-worker.dll> --python <pinned-python>
--python-worker <cpython-worker.py> --out <new-receipt.json> --case <case-id>`
inside an owned service with `RuntimeMaxSec=30`, `TimeoutStopSec=0` and
`KillMode=control-group`, holding the shared VM lease. Never overwrite a receipt.

The dedicated VM is the same AMD EPYC 9V45, four-core Ubuntu 24.04.4 machine as
the milestone. Toolchains remain SDK **10.0.401**, runtime **10.0.12**, optimized
PGO/LTO CPython **3.13.16** with normal GC/GIL and its experimental JIT disabled.
Release/library identities, ordinary worker settings and clocks are in receipts.

| Evidence | SHA-256 |
| --- | --- |
| Diagnostic catalog | `49b6048ccedcf86fd8df279fda5afa988af811acdb4737ebfe5aa0d8e475aad9` |
| Baseline Lython library | `6921649f8a4ef5894c2094c3e50b4d4c71b2d56b00642e6451a3561220e4c93d` |
| Candidate Lython library | `177ca5fbccc6aef4c6adbbe83cff62ce12bdf213f4a1ffa02a8d8f9b076cec7b` |
| Baseline worker | `a1c4ad8e3914962e78cdc7b46604a63b75b3d0f9f158ecfa981bb8f1db81bcb4` |
| Candidate worker | `a4289f33faa219052de7f50a1c039d2d92686f6421a317103c22c5d778b4e4be` |
| CPU trace | `bb5980c7de185f5db1e26175e46a07224c5fc50bf187d74915638fa71f6e596a` |
| Pipeline repetition 1 receipt | `aa78ae6de66fb60cb0d80cd30f5e8479c742c72ae861d8c6fa96da6bfc6f2484` |
| Pipeline repetition 2 receipt | `ce7adfdf355cdd04fb978098f510c3dd59f49c67c6b471ed49e3efbd5e1f7bf9` |
| Split/count repetition 1 receipt | `6bc8bfa925b4d2be9642d4536f9dc7684aa6d9a1a11a2bdeda67fd18d365b328` |
| Split/count repetition 2 receipt | `e2cec3cdfcec86fddd1832802bcef2fab0fa99eaeb195f6b2dd6241a432a99d9` |

Raw declarations, all nine micro receipts, trace, source snapshots, service
receipts, logs and independent response/hash audit are retained locally under
`.git/agent-notes/ascii-pipeline-20261009/`. The audit checks every successful
response's counts and source/fixture/golden/actual digests, seven measurement
batches per engine and both verifications. It also validates all 7,300 invocation
results from the separate profiling worker, including warmup and verification.

Focused local Debug/Release checks each passed **144** string, memory, ownership
and reclamation tests, including four new regressions for identity deduplication,
denied growth/retry, unpublished refunds and collection of dropped strings.
The independently built Linux Release producer passed those 144 tests and all
373 comparison tests before timing. Full local Debug and Release each passed
**9,044 tests** (1,310 white-box and 7,734 public API), after explicit matching
probe builds. Local correctness checks used the installed preview SDK; timing
used only the pinned, independently built Linux Release artifacts above. The
diagnostic catalog generator reproduced the timed catalog byte-for-byte.
