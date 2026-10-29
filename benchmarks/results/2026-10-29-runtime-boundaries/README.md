# Runtime phases and genuinely delayed host work

One fixed diagnostic collection on **2026-10-29**, source **1c5cff2a59d1defd94049e70940267a569cc717a**,
core 0.11.0 development, .NET 11 RC3 SDK/runtime 11.0.100-rc.3.26522.148.
The same dedicated Azure Standard_F4as_v7 Ubuntu 24.04.4 machine, four EPYC 9V45
vCPUs, 16 GiB RAM and no swap. Both quiet gates pass. Collection: **8.31 seconds**,
ordinary GC/tiering, no runtime override. **No performance-qualified result.**

The six unchanged scripts/inputs/full stdout and file goldens come from the
[foundation catalog](../2026-10-29-foundation/representative.json). This experiment
adds no production hooks or public options and does not compare CPython. The
[primary comparison](../../../docs/BENCHMARKS.md) retains its own source and clock
boundary. Do not compare numbers here directly with its historical medians.

## Exclusive synchronous phases

Eight warmups and sixteen observations per source. Reflection invokes the actual
context constructor, module annotation initialization, executable entry,
outstanding writer publication, public projection and hash cleanup on the existing
large-stack worker. It uses the real compilation/execution/result owners. The
success-only experiment requires normal completion and verifies full results;
it is not an alternative failure-handling backend.

Medians in microseconds. Guest/publication excludes the matching nested host
intervals **per observation before summarizing**. It includes imports, function
frames, dispatch, native builtins, allocation/admission and other guest work.
These are wall-time partitions, not CPU shares. Individual column medians do not
sum to a median total.

| Workload | Handoff | Context/setup | Guest/publication excluding host | Host | Projection | Guest IQR |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Config edit, small | 11.6 | 33.0 | 186.5 | 1,299.4 | 2.1 | 6.3% |
| File search, large | 15.0 | 30.6 | 4,040.0 | 96.7 | 1.9 | 6.9% |
| CSV helpers, large | 16.7 | 36.8 | 28,161.2 | 146.2 | 3.2 | 57.1% |
| Text helpers, large | 13.4 | 36.8 | 8,240.7 | 97.7 | 0.9 | 3.8% |
| JSON-lines helpers, large | 13.2 | 33.1 | 9,734.0 | 138.3 | 0.8 | 7.3% |
| File manifest helpers, large | 11.0 | 11.0 | 77.6 | 195.9 | 0.6 | 5.3% |

Context creation allocates 15,760 bytes here (19,440 for text/JSON). Execution
allocates about 9.90 MB for CSV, 5.51 MB for text and 7.85 MB for JSON-lines;
projection allocates 368 bytes. Current-thread counters apply only to these
synchronous phases and include reflection/counter overhead. Result disposal
medians are 0.1–0.3 microseconds with zero current-thread allocations; execution
owner cleanup is recorded separately. Full distributions, publication, hash
cleanup and retained capacities are in [summary.json](summary.json).

These observations argue against setup/projection as the dominant cost of the
large aggregation cases. They do **not** attribute all guest cost to instruction
dispatch or the governor. Config/manifest host work dominates their small
execution boundaries. CSV is unstable; its median is not a stable new regression
or throughput estimate. Ordinary tiering/warmup, reflection and the decorated host
differ from the duration-based micro tool. No favorable remeasurement follows.

## Public APIs and actual awaited host delay

The public lanes call unmodified Run/RunAsync and the same filesystem host through
a timing decorator. The delayed lane injects one awaited `Task.Delay(2)` before
the first I/O, then performs the identical host operation. All operations and
cleanup finish before proceeding. The 96 recorded delayed invocations each make
one such pause and return an incomplete public task; measured pause medians are
2.078–2.110 ms. This is a controlled asynchronous scheduling experiment, not a
claim about a particular storage service or cancellation latency.

Elapsed medians in milliseconds. Reset/full result verification are outside
these clocks, unlike the primary complete-job comparison. Execution-owner
cleanup is inside; final result disposal is separate. All samples remain visible.

| Workload | Run | RunAsync, completed host | RunAsync, delayed host |
| --- | ---: | ---: | ---: |
| Config edit, small | 1.530 | 1.550 | 2.478 |
| File search, large | 4.209 | 4.923 | 6.990 |
| CSV helpers, large | 25.141 | 32.017 | 34.302 |
| Text helpers, large | 8.235 | 9.298 | 11.658 |
| JSON-lines helpers, large | 9.830 | 11.629 | 13.581 |
| File manifest helpers, large | 0.299 | 0.321 | 2.399 |

Do not subtract the completed/delayed medians to estimate continuation overhead.
The injected pause also changes scheduling/tiering and the config case is a
clear example of why unrelated median subtraction is misleading. Public
allocations are process-wide diagnostic counters, not exclusive thread/phase
attribution; unrelated CLR work may contribute.

## Ownership, reproduction and stopping point

All **576 invocations**, including warmups, match complete goldens. Every execution
and result returns to its compilation baseline. The projected results keep
36–42 admitted bytes after execution; disposal releases them. All six compilation
owners then return to zero. No collection is forced and no background operation
is abandoned. These are accounting estimates, not physical heap sizes.

[collect.py](collect.py) holds the existing VM lease across build/quiet checks and
collection. Each diagnostic runs inside an owned systemd control group with a
prospective 60-second limit; machine checks use twenty seconds. The
[success-only reflection source](diagnostics/Program.cs) builds against the exact
candidate core DLL via `LythonCorePath`; it adds no runtime dependency.
[analyze.py](analyze.py) describes every recorded observation without rejection.
Sources and raw records are frozen evidence, not supported instrumentation.

A local functional setup run initially failed the existing fixture-directory
prefix guard before any workload. The prefix was corrected and the local setup
completed; its busy-workstation times were not used. The VM collection succeeded
once. No rerun was made to stabilize CSV or select favorable samples.

This closes the bounded M3 phase/suspension investigation. Together with the
existing CPU/allocation profiles, it prioritizes invocation-internal value/call
transport and dispatch over further startup/setup micro-optimizations. It neither
revives the rejected executable-class migration nor removes current ownership or
managed stack protections. Stable final-release comparison remains a separate
delivery decision.
