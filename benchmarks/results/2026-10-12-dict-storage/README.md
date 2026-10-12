# Live dictionary storage ownership, 2026-10-12

Dictionary containment now uses a **128-byte allowance per retained entry**,
alongside existing backing/protocol charges and separate large-payload owners.
Replacement transfers entry funding; deletion releases it regardless of lookup
key identity. Attachment admits existing storage before publishing its governor.
This removes **223 net production lines**, including dictionary scalar identity
maps, pair-adoption rollback, 119 unused bulk/refcount lines and duplicate wrapper
notifications. Property's remaining scalar coupons and forced GC are separate work.

Wrapped dictionaries retain their existing charged wrapper owner. Views and
iterators therefore keep live storage funded, and all child mutations refresh
that one snapshot. The parent reference creates an ordinary collectible cycle;
tests verify collection after the last alias drops. No new registry, runtime
option, frame/context pooling or capability was introduced. Host mediation,
cooperative cancellation, allocation preflights, cycles, stack admission and
bounded public projection remain.

Aliases and nonnumeric entries now pay the same conservative allowance. Peaks
can increase and formerly accepted tight-budget scripts can deny earlier.
This counts live retention: bounded delete/refill and dropped-view streams still
complete under fixed limits. Callback side effects and inserted streaming
prefixes remain visible after errors.

## Original faults and checks

Original production is `58f504f52465e6c86c55cf54fde7ebf19f28bca7`. Frozen original
white-box producers demonstrate four dictionary faults, four stale wrapper
release snapshots, and twelve escaped view/iterator lifetime failures. They
include an uninserted key retaining 64 bytes after value admission denies,
equal-distinct key deletion retaining charges, unadmitted first attachment, and
wrapper reclamation releasing 448 bytes instead of its remaining 384 bytes.
The property registration-refund control passes and remains preserved.

Actual public probe binaries also demonstrate 2,000 empty-dictionary delete/pop
cycles under a 65,536-byte cap: original-key deletion succeeds at a 681-byte peak,
while equal fresh keys exhaust memory at 65,504 bytes. All eight sync/async
variants succeed in the candidate under that same cap. Original DLLs, sources,
hashes, outcomes and caps are retained.

Qualified root source is `bd7e51bbfc32577b909a7a5280542b7a72c54c1c`, production
`5f6bf332759cab2cfb126960d0e12182579db842`. Complete Debug suites pass **10,217
checks** (2,023 white-box, 8,194 public). Focused Release suites pass **161 Windows
checks** and **158 Linux checks**. Validation covers entry denial/refund,
replacement at a full budget, Boolean/integer key twins, copy and attachment
failure, unrelated pinned storage, protocol callbacks, global namespaces,
retained/dropped rows, escaped views/iterators and genuine host suspension.

The first complete gate retains eight old fee/peak/population assertions. Original
10.7 MB/11.534336 MB dictionary caps remain explicit earlier insertion-denial
controls. Additional snapshot/ordinary cases fund exactly the 6.4 MB allowance
delta for the same 100,000 entries, preserving the 1,600,032-byte snapshot denial.
The retained six-column CSV check applies its original transient bound after
subtracting only the declared 307,200-byte allowance for its 400 retained rows.
Its execution limits are unchanged. Generic-method callback steps, reserved
bytes, recursion and class-cell observations remain exact; annotation entries
explain the new 128/256-byte trace deltas.

Overbroad and slow test-fixture groups were stopped within their declared bounds;
partial results are not qualification. Incorrect synchronous use of an async
host is retained as a test-harness failure. Final fixtures use matching hosts,
one genuine initial suspension and controlled collection at stdout markers,
with the original 200×64 loops and 1 MiB limits. They are partitioned into bounded
groups. Initial VM script generation failed before launching a service; no timing
measurement was retried or discarded.

## Short comparative measurements

Eight fixed collections cover two passes of four cases, taking **7–8 seconds
each including cleanup**, with 348 verification/warmup/calibration/measurement
attempts and 168 measured samples. Separate ordinary workers use .NET 10.0.12
and CPython 3.13.16. Worker tiering, GC settings and guards remain normal; the
supervisor's tiering override is removed before worker launch. No full lane ran.

The preceding frozen baseline is `24a6afda`, with the same production tree as
`e1efc58f`; candidate workers are built from `bd7e51bb`. Actual library/worker
hashes, source identities, equivalent outputs and termination receipts reconcile.

| Case | Pass 1 change | Pass 2 change |
| --- | ---: | ---: |
| Empty control | +1.48% | +0.05% |
| Integer loop, 16,384 iterations | +0.83% | −0.27% |
| CSV DictReader discard, 128 rows | +6.22% | +2.76% |
| Numeric dictionary, 1,024 entries | −16.89% | −16.72% |

Dictionary medians move **234.38 → 194.79 µs** and **238.04 → 198.24 µs**;
corresponding CPython medians are 25.01 and 25.22 µs. This supports a targeted
short-round improvement, without certified milestone ratios or a general speedup.
Retain the slower CSV results: spreads are 10.70%/3.73% and 0.38%/7.02% IQR.
The first integer-loop control is also slower with low spread (0.34%/0.83%).
Allocated-byte savings and forced-GC pause reduction were not measured.

All owned services/workers are stopped, groups are empty, source checkouts are
clean, qualified assembly hashes reconcile and the VM lease is free. Raw
failures, original/candidate outcomes, samples, contracts and cleanup proof are
retained in [evidence.json](evidence.json).
