# Execution-check cold paths

Execution checks now keep periodic sweeping and exception construction in separate
non-inlined helpers, with the small checkpoint body eligible for aggressive
inlining. Two short replicas show **10.9–11.3% less integer-loop time**,
**5.7–9.4% less positional-call time** and **3.5–4.2% less keyword-call time**.
The change is integrated. Sort medians are higher with substantial variation;
both observations remain visible below. Empty/pipeline controls have small mixed
changes. No full benchmark lane runs.

All twelve prospectively declared micros take **6.41–7.08 seconds**, and whole
owned services stop in **8.04–8.05 seconds**, under a **30-second process-group cap**
including cleanup. These are diagnostic comparisons; milestone qualification and
certified Lython/CPython multipliers remain deferred.

## Source and behavior

Frozen baseline `4b9748b33f0504826753e3eab44ee0c7fb7a9f54`, production tree
`a3b7fa7d5c24f735ea5f101ae3e034d2d0298e4f`, is identical in production to delivered
green head `8371dc38`. Its prepared Release worker is rehashed and reused.
Candidate `c9761012e2eaa85f04aaff68bdf6e33f6fb3f88e` has production tree
`1b5513146e880aba075207ab3ba365006d6ee3b5`. Test-only prerequisite `fc28b476`
adds three guard cases and strengthens dormant-fuel checking; candidate tests are
`4873ff730fb2c6aa1ae7746ea083ed2f909a7b91`. The whole benchmark project remains
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

Every checkpoint still decrements the sweep counter, sweeps on checkpoint 256
with reset before sweeping, then checks optional fuel, then cancellation. Fuel
counts only when enabled. Exhausted fuel retains priority over simultaneous
cancellation. Limit values are still read every checkpoint; messages and source
spans are retained. Ordinary memory limits, resource ownership and host mediation
remain active. The experiment tests this combined code layout and inlining change;
it does not isolate each JIT contribution or claim reduced guest allocation.

Separate baseline call captures, collected before candidate preparation, identify
execution checks as 7.02% / 7.70% of exclusive CPU samples. Their whole groups take
14.08 / 14.08 seconds under 30-second caps; 872 responses / 27,720 complete guest
invocations are audited. They have 1,054 / 1,104 samples, zero reported losses or
missing stacks, and 5.88% / 7.43% unresolved leaves. Inclusive stacks overlap;
profiler/kernel/JIT overhead is visible. Allocation ticks are sampling evidence,
and GC event pairs are incomplete. These captures identify a design lead, predict
no savings, and establish no qualified before/after CPU distribution or complete
GC counts/pause durations.

## Complete invocation timings

Both Release producers use SDK 10.0.401 / CLR 10.0.12 on the quiet dedicated Ubuntu
VM. Isolated release CPython 3.13.16 keeps ordinary GC and GIL-enabled execution.
Lython workers retain ordinary runtime/governor defaults; only the separate
controller disables tiering. No tracing, builds, tests or downloads overlap
normal timing on the VM.

Canonical sources, fixtures and complete output goldens are identical on all
engines. Each invocation constructs fresh guest state and complete inputs. The
loop runs 16,384 iterations; call jobs execute 2,048 guest calls plus their loop
and checksum. Sort stably orders 2,048 tuples and prints all 21,419 output bytes.
These are complete job timings. Full catalogs are byte-identical and both
producers explicitly verify all six cases against CPython before timing.

Each engine receives one second of warmup, roughly 25 ms calibration and seven
rotating-order measured rounds. Cheap success/full-output checks remain timed;
compilation, transport and output hashing remain outside the worker clock.
Every replica is retained. Cells show **median microseconds/invocation** and
**IQR/median**.

| Case / replica | Before | After | CPython |
|---|---:|---:|---:|
| Empty / 1 | 23.359 (3.4%) | 23.139 (2.7%) | 1.482 (0.5%) |
| Integer loop / 1 | 1434.141 (0.7%) | 1277.506 (0.9%) | 617.424 (0.1%) |
| Positional calls / 1 | 712.885 (2.3%) | 645.914 (1.6%) | 119.929 (0.4%) |
| Keyword calls / 1 | 745.218 (0.9%) | 713.827 (0.9%) | 136.372 (0.3%) |
| Canonical full sort + output / 1 | 2670.421 (6.8%) | 3047.337 (18.5%) | 300.746 (0.3%) |
| ASCII pipeline / 1 | 138.734 (2.2%) | 139.880 (3.6%) | 41.590 (0.4%) |
| Empty / 2 | 23.045 (4.1%) | 23.286 (1.7%) | 1.480 (0.3%) |
| Integer loop / 2 | 1481.962 (0.7%) | 1315.083 (1.3%) | 629.181 (0.1%) |
| Positional calls / 2 | 721.940 (1.7%) | 680.538 (1.7%) | 122.546 (0.4%) |
| Keyword calls / 2 | 744.004 (2.2%) | 718.034 (1.6%) | 134.824 (0.2%) |
| Canonical full sort + output / 2 | 2606.902 (8.0%) | 2662.586 (5.9%) | 310.240 (1.0%) |
| ASCII pipeline / 2 | 140.591 (1.9%) | 138.967 (2.1%) | 41.670 (0.3%) |

Changes in elapsed time are loop -10.92% / -11.26%, positional
calls -9.39% / -5.73% and keyword calls
-4.21% / -3.49%. These target improvements repeat and exceed
each replica's measured spread. Acceptance rests on those gains and correctness.
Empty -0.94% / +1.05% and pipeline
+0.83% / -1.15% are small mixed changes.

Sort medians rise +14.11% / +2.14%. Replica 1 has 18.54%
candidate spread / 6.82% baseline spread; replica 2 has 5.90% / 7.99%.
These short observations do not establish a reliable material sort regression;
they remain a limitation to review at a future milestone. No observation is
discarded or recollected to chase a gain. The remaining CPython gap and frame
storage design costs remain open.

## Correctness and audit

The guard tests pass on unchanged production in Debug and Release (seven selected
checks each). With the candidate, matching Debug probe and the full frozen Debug
suite pass **9,164 checks** (1,407 white / 7,757 public) before timing is declared.
Matching VM Release probe and **111 selected white / 286 selected public Release**
checks also pass. Coverage includes exact sweep cadence and fuel/cancellation
priority, instruction lowering, generators, synchronous/asynchronous cancellation,
bounded iteration/numeric work, memory limits, scopes and streaming.

Independent audit recalculates every median/IQR, verifies **514 protocol responses**,
request IDs/counts, complete outputs, source/fixture/catalog hashes, loaded source
versions, SDKs and ordinary worker settings. TRX completion times and frozen Debug
binary hashes prove the full gate precedes the prospective timing declaration.
Preparation has separate 180-second group bounds; it is not a benchmark lane.
Service journals prove collection starts after declaration and cleanup within cap.

Both producers are rehashed and source trees are clean. All **14 owned services**
are terminal, **50 recorded preparation/controller/worker PIDs** are absent, and
the shared lease is freshly available. Baseline profiles have their separate
proof: two terminal services and six recorded controller/worker/tracer PIDs absent.
No failed correctness or timing attempt is retried.

Private declarations, receipts, TRX, journals and cleanup proof remain under
`.git/agent-notes/checkpoint-cold-path-20261010/`; baseline CPU evidence is under
`.git/agent-notes/call-cost-after-array-20261010/`. Maintained
[evidence](evidence.json) retains all observations and supporting receipt hashes.
Original CSV/pipeline investigations, external Utf8Regex integration and matched
milestone/secondary qualification remain separate pending work.
