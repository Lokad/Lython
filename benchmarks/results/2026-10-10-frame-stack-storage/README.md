# Embedded operand-stack storage

The operand stack's count and array reference now live inside each executable
interpreter. Complete positional/keyword call jobs use **about 3.7% less managed
allocation** in two fixed-count passes. The change is integrated as a storage
improvement. Call timings are mixed; no reliable throughput gain is established.
The remaining call gap against CPython stays open.

Twelve prospectively declared micros take **6.38–7.12 seconds** each, with whole
owned services stopping in roughly **8.04 seconds**. Two separate allocation
groups stop in **4.04 / 4.03 seconds** after the normal micros end. Every collection
uses a **30-second external process-group cap including cleanup**. No full lanes
run, and milestone qualification remains deferred.

## Source and design

Frozen baseline `c9761012e2eaa85f04aaff68bdf6e33f6fb3f88e`, production tree
`1b5513146e880aba075207ab3ba365006d6ee3b5`, is identical in production to delivered
green head `ea6d5297`. Its prepared Release producer is rehashed and reused.
Candidate `17a6b046b9208b7cc4f835253cc28e273dbbabf3` has production tree
`47e033c1c653812e18adec388ca0fc41f3e48337`. Tests remain
`4873ff730fb2c6aa1ae7746ea083ed2f909a7b91`; the whole benchmark project remains
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

Previously each interpreter owned a separate stack object, holding its array
reference and count. Those fields now form a mutable value inside the interpreter.
All 13 helper parameters borrow that value by reference, preserving mutations in
exception routing, calls, collection builders and synchronous/asynchronous handlers.
The interpreter field is mutable; there are no readonly defensive copies.

Minimum capacity, growth, indexing, push/pop/peek, cleared popped slots and tail
disposal of executable temporaries retain their previous operations. Each
generator retains its own interpreter and embedded stack through suspension,
send/throw/close and asynchronous resumption. Backing arrays, frame/cell ownership,
logical charges, checkpoints, limits and host mediation retain their contracts.
The experiment eliminates a storage object without introducing frame or array
pooling. It does not change the benchmark harness or claim exact allocation by
guest type.

Two previously collected baseline call profiles identify interpreter/context/
frame/stack storage and object arrays as repeated allocation leads. They have
1,007 / 1,094 CPU samples, zero reported losses or missing stacks, and
7.05% / 7.50% unresolved leaves. Complete frame execution appears in 73.68% /
73.49% of inclusive samples. Those stacks overlap; profiler/kernel/JIT overhead
is visible, allocation ticks are sampling evidence, and GC event pairs are
incomplete. This lead predicts no saving and supports no qualified before/after
CPU distribution, exact per-type allocation or complete GC count/pause claim.

## Ordinary complete-job timings

Both producers use Release SDK 10.0.401 / CLR 10.0.12 on the quiet dedicated Ubuntu
VM, with ordinary runtime and governor defaults. Isolated release CPython 3.13.16
retains ordinary GC and GIL-enabled execution. Only the separate micro controller
disables tiering. No tracing, builds, tests, transfers, downloads or allocation
diagnostics overlap normal timing on the VM.

Canonical sources, fixtures and full output goldens are identical across engines;
full catalogs are byte-identical. All six cases explicitly verify against CPython
on both producers before timing. Each invocation constructs fresh guest state and
complete inputs. The loop runs 16,384 iterations; calls perform 2,048 guest calls
plus their loop/checksum. Sort stably orders 2,048 tuples and prints the complete
21,419-byte result. These are complete job timings.

Each engine receives one second of warmup, roughly 25 ms calibration and seven
rotating-order measured rounds. Cheap success/full-output checks remain inside
the clock; compilation, transport and output hashing remain outside. Every
replica is retained. Cells show **median microseconds/invocation** and
**IQR/median**.

| Case / replica | Before | After | CPython |
|---|---:|---:|---:|
| Empty / 1 | 23.275 (2.6%) | 23.510 (4.9%) | 1.510 (0.3%) |
| Integer loop / 1 | 1322.168 (1.1%) | 1292.233 (0.9%) | 641.718 (1.6%) |
| Positional calls / 1 | 665.448 (1.0%) | 662.671 (0.3%) | 122.467 (0.3%) |
| Keyword calls / 1 | 704.059 (1.1%) | 712.084 (0.8%) | 135.310 (0.5%) |
| Canonical full sort + output / 1 | 2568.264 (5.5%) | 2696.986 (6.0%) | 301.261 (0.2%) |
| ASCII pipeline / 1 | 139.312 (1.5%) | 138.900 (2.4%) | 41.294 (0.3%) |
| Empty / 2 | 23.551 (3.5%) | 23.102 (6.9%) | 1.490 (0.3%) |
| Integer loop / 2 | 1299.809 (0.4%) | 1292.990 (0.8%) | 587.105 (0.2%) |
| Positional calls / 2 | 654.941 (1.2%) | 667.712 (0.6%) | 119.738 (0.3%) |
| Keyword calls / 2 | 692.127 (1.0%) | 683.551 (0.4%) | 135.563 (0.5%) |
| Canonical full sort + output / 2 | 2610.072 (8.8%) | 2565.639 (1.6%) | 309.398 (0.7%) |
| ASCII pipeline / 2 | 138.748 (2.0%) | 139.381 (1.2%) | 41.196 (0.4%) |

Elapsed-time changes: loop -2.26% / -0.52%, positional calls
-0.42% / +1.95%, keyword calls +1.14% / -1.24%,
empty +1.01% / -1.91%, sort +5.01% / -1.70%
and pipeline -0.30% / +0.46%. Call/control/sort changes
are mixed. The loop's second gain is below its candidate spread. These observations
establish no reliable throughput gain or repeatable material regression. Acceptance
rests on the repeated managed call-storage saving and correctness.

## Separate complete-invocation allocation diagnostic

A standalone helper loads each exact frozen comparison worker and uses its
existing Compile/Invoke boundary. It checks complete output every invocation,
compiles outside measurement, performs 16 warmup invocations and measures 100
complete invocations per case/pass using `GC.GetTotalAllocatedBytes(precise: true)`.
Each producer runs two passes in its own fresh process. This counts process-wide
managed allocation, including background runtime activity; it is not exact
accounting by guest type. Ordinary GC/tiering remain enabled and no collection
is forced. There are **2,400 measured / 384 warmup complete invocations**.

Cells show **managed bytes/complete invocation**. Positive savings mean fewer bytes.

| Case / pass | Before | After | Savings |
|---|---:|---:|---:|
| Empty / 1 | 29,440.00 | 29,416.00 | +0.082% |
| Integer loop / 1 | 1,079,512.40 | 1,079,488.40 | +0.002% |
| Positional calls / 1 | 1,326,654.72 | 1,277,471.52 | +3.707% |
| Keyword calls / 1 | 1,343,293.44 | 1,294,103.36 | +3.662% |
| Canonical full sort + output / 1 | 3,543,486.88 | 3,567,014.88 | -0.664% |
| ASCII pipeline / 1 | 461,896.00 | 461,872.00 | +0.005% |
| Empty / 2 | 29,272.00 | 29,248.00 | +0.082% |
| Integer loop / 2 | 1,079,299.68 | 1,079,275.68 | +0.002% |
| Positional calls / 2 | 1,326,463.36 | 1,277,287.36 | +3.707% |
| Keyword calls / 2 | 1,343,125.20 | 1,293,949.20 | +3.661% |
| Canonical full sort + output / 2 | 3,486,392.88 | 3,486,368.88 | +0.001% |
| ASCII pipeline / 2 | 461,896.00 | 461,872.00 | +0.005% |

Positional calls save 3.707% / 3.707%, keyword calls 3.662% / 3.661%, approximately
49,176 bytes per complete job. Empty, loop and pipeline save 24 bytes in each pass.
Sort allocation rises 0.664% in its first pass and falls by 24 bytes in its second;
both observations remain visible. No sort-allocation improvement is established.
The two passes share their producer process; no independent-session qualification
is claimed. No result is discarded or recollected.

## Correctness, retained preparation failure and cleanup

Matching Debug probe and full frozen Debug suite pass **9,164 checks**
(1,407 white / 7,757 public) before collection declarations. Matching VM Release
probe and **111 selected white / 286 selected public Release** checks pass.
Existing semantic coverage exercises stack builders, routing and finally cleanup,
generators and retained cells, synchronous/asynchronous execution, cancellation,
memory limits, scopes and streaming. Test sources are unchanged.

The allocation helper builds successfully once under its own 180-second owned
cap. Its launcher then records exit 5 because an EXIT trap tries to stop the
already-unloaded service again. The original failed launcher receipt is retained.
A separate recovery verifies the successful build's journal, terminal group,
absent recorded PID, hashes and lease. The existing frozen helper is reused.
An initial local declaration guard incorrectly expects exit 1 and fails before
any diagnostic worker starts; its rejected transfer cannot launch a collection.
The expectation is corrected to the recorded exit 5. No helper build, diagnostic
worker or benchmark collection is repeated to hide these preparation errors.

Independent audit recalculates all medians/IQRs and checks **518 normal-micro
responses**, request IDs/counts, complete outputs, hashes, loaded versions, SDKs
and ordinary worker settings. TRX completion times and frozen binary hashes prove
the full gate precedes declaration. Allocation audit checks all 24 diagnostic
rows, exact frozen producer/helper hashes, fixed counts and default settings.
Journals prove prospective starts, separate timing/diagnostic schedules and
whole-group caps. Both producers are rehashed and source trees are clean.
All **17 owned services** are terminal, all **55 recorded preparation/controller/
worker/helper PIDs** are absent, and the shared lease is freshly available.

Raw declarations, receipts, TRX, journals, retained preparation failures and
cleanup proof remain private under `.git/agent-notes/frame-stack-field-20261010/`.
Baseline CPU evidence is under `.git/agent-notes/call-cost-after-checkpoint-20261010/`.
Maintained [evidence](evidence.json) retains every observation and supporting hash.
Original CSV/pipeline investigations, external Utf8Regex integration and matched
milestone/secondary qualification remain separate pending work.
