# List reclamation registration — 2026-10-10

The accepted change keeps each list's reclamation entry beside its owner,
avoiding a second ephemeron handle in the process-wide ConditionalWeakTable.
The pool still holds a weak target and charges the same **128-byte** entry fee.
Identity deduplication, backing snapshots, mutable growth, refunds, denial/retry,
collection, deferred split transfer and host-mediated boundaries remain intact.
Other value types retain their existing registration paths.

Short comparisons show scalar CSV taking **22.36%/15.96% less time** and
retained-output CSV **13.02%/4.86% less time**. The second retained-output
candidate has **11.18% IQR/median**, so retain that variation. Loop controls rise
**0.74%/1.03%**, empty controls vary **+2.69%/−2.31%**, and ASCII pipeline latency
falls **1.33%/1.50%**. These are diagnostic observations supporting this round,
with no qualified Python multiplier or full benchmark lane.

## Evidence behind the experiment

Four separately declared ten-second CPU/GC profiles precede the list change.
They compare registry-funding source `c80ad2ab` with CSV-ownership source
`d4a255fb`, the subsequent timing baseline. Initial profiles have **83.0%/80.3%**
unresolved leaves. Matching native symbols for pinned .NET **10.0.12** were then
downloaded with pinned dotnet-symbol **10.0.750501**, without changing executable
hashes. Separate captures resolve nearly all leaves, with no lost events or
missing stacks. Original captures and every response remain preserved.

| Native-symbol profile | Samples | Unresolved leaves | Handle scan exclusive | Parser inclusive | Registration inclusive | StringIO render inclusive |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Registry-funding source | 5,047 | 1.23% | 43.27% | 46.23% | 17.93% | 18.47% |
| CSV-ownership source | 4,147 | 1.62% | 34.80% | 55.39% | 20.62% | 14.44% |

The handle-scan leaf is `ScanConsecutiveHandlesWithUserData`. Inclusive shares
overlap and must not be added. These instrumented profiles identify GC handle
processing as a useful design lead; they neither measure normal latency nor
quantify the cost of one handle type or explain the earlier mixed CSV/pipeline
timings. The accepted list source has not been profiled here. Symbol placement
follows [Microsoft's collect-linux documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace#get-symbols-for-native-runtime-frames).

Independent auditing verifies **3,789 profile responses** and
**120,880 complete output-checked invocations**, source/library/input
hashes and the 30-second process-group caps. Each complete profile collection
takes about fifteen seconds, including warmup, attachment and shutdown.

## Representation and validation

Production change `ff89954b` adds one eight-byte entry reference to a list.
A fixed-count 10,000-object diagnostic measures **176.0152 → 184.0152 bytes**
per empty list including its storage, below the unchanged **192-byte** governed
construction charge. Setup, the retaining array and warmup precede the allocation
counter; all measured lists remain alive. This is a private layout diagnostic,
with no timed public invocation or claim about total workload allocation.

Four added white-box controls verify shared registration for aliases,
independent copy/mutation ownership, denied registration followed by retry,
unpublished refunds and weak lifetime. The first VM preparation fails five
existing coupon tests because their snapshot helper directly reads the old
table. Test-only follow-up `39952af6` reads the registry's shared lookup and
preserves all assertions. That failed producer and its receipt remain retained;
no timing was collected from it. Production trees are identical across the
initial and final source; final timing uses the corrected test/source producer.

Final focused checks pass **132 each in Debug and Release**. The unchanged
candidate production/public-test trees pass **785 selected public Release
checks**. Final full Windows Debug passes **9,124** (1,373 white-box / 7,751
public); Linux Release passes **1,373 white-box and 577 selected public checks**.
Matching probes were built explicitly before public checks. Original C05
retained-row failure and the original pipeline variation remain unexplained.

## Short comparisons and cleanup

Baseline `d4a255fb0bb5f67d48993ee57c451f9e5ce4bab6` matches the production tree of delivery `570aa795`.
Final candidate `39952af6e4c645c0733e6eda771cc513ecea16f0` matches the delivered production and test trees.
All seventeen catalog cases remain byte-for-byte equivalent. Both Lython
producers use Release SDK **10.0.401**/.NET **10.0.12**, ordinary worker tiering,
workstation/Interactive GC, default limits and unset instruction fuel, with no
runtime overrides or forced collection. CPython **3.13.16** uses the pinned
PGO/LTO build, ordinary GIL/GC and isolated mode.

Ten prospectively declared collections take
**6.41–6.99 seconds** each, with owned 30-second
process-group caps and zero stop timeout. All **441 responses** pass counts
and source/fixture/golden hash checks. Seven measured batches and two correctness
verifications per engine per collection yield **30 independently recomputed
medians/IQRs**. No favorable recollection or full lane ran.

| Case | Repetition | Baseline µs | Candidate µs | Candidate latency change |
| --- | ---: | ---: | ---: | ---: |
| control.empty.control | 1 | 22.920 | 23.535 | +2.69% |
| loops.integer.large | 1 | 1631.431 | 1643.481 | +0.74% |
| strings.pipeline-ascii.medium | 1 | 138.194 | 136.352 | -1.33% |
| csv.retain.medium | 1 | 531.283 | 462.129 | -13.02% |
| diagnostic.csv.scalar-count.medium | 1 | 237.725 | 184.575 | -22.36% |
| control.empty.control | 2 | 23.806 | 23.257 | -2.31% |
| loops.integer.large | 2 | 1622.922 | 1639.664 | +1.03% |
| strings.pipeline-ascii.medium | 2 | 139.617 | 137.524 | -1.50% |
| csv.retain.medium | 2 | 519.589 | 494.330 | -4.86% |
| diagnostic.csv.scalar-count.medium | 2 | 226.391 | 190.255 | -15.96% |

[micro-results.csv](micro-results.csv) includes every engine and its spread;
[evidence.json](evidence.json) records source, test, layout, profile and receipt
hashes. All sixteen owned preparation/profile/micro services are terminal with
no main process, including the preserved failed preparation. Every recorded
worker/tracer is absent, files and runtime/symbol hashes match, all four frozen
checkouts are clean and the shared VM lease is free. Full comparison lanes remain
reserved for infrequent declared milestones.
