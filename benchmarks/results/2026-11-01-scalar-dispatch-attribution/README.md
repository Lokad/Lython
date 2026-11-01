# Actual integer-loop and complete CSV attribution (B71)

2026-11-01, source `d703767c`; core/worker source is unchanged from `9003d045`.
The ordinary adapter uses 0.4.0. No development regex producer enters this run.
One successful pair records **6.006 seconds of CPU capture in a 14.078-second
owned service**. No runtime change, recapture, throughput gain or CPython ratio.

## Why this workload

The maintained numeric report loads JSON and calls sum. Its native accumulator
already keeps machine-integer/float prefixes and exact promotion; it does not
exercise guest arithmetic per input element. B70 closes that misleading source
hypothesis using existing B52 evidence, without another numeric-report capture.
This pair instead executes `loops.integer.large`: a 16,384-iteration range loop
with `total += i`, plus the complete ordinary CSV aggregation control (4,096
records / 57,361 input bytes). Sources, fixtures and independent goldens are frozen
in [catalog.json](catalog.json). Pinned isolated CPython3.13.16 verifies both full
stdout/file goldens; no CPython timing is collected.

## Boundary and actual ownership checks

Dedicated Ubuntu24.04.4 Azure F4as_v7, four EPYC9V45 vCPUs/16GiB, actual .NET11RC3,
ordinary worker GC/tiering. Existing framed worker and perf cpu-clock/fp at199Hz,
CLR maps,128 warmups and2s settling per case. Each capture makes single complete-
job requests until its3s deadline, independent of a large fixed batch. Total
capture limit8s, successful service limit24s. Compilation/startup are outside;
mediated I/O, full verification and declared cleanup remain in the capture boundary.
The private unchanged-worker wrapper adds a final shared-budget-zero witness.

| Case | Capture s | Completed jobs | Guest CPU s / samples | Unknown leaves | Tiered-JIT CPU s |
| --- | ---: | ---: | ---: | ---: | ---: |
| Integer loop | 3.00181 | 2,521 | 2.53 / 502 | 24 | 0.54 |
| Complete CSV | 3.00401 | 380 | 2.13 / 422 | 30 | 0.79 |

The actual named execution thread identifies guest events; all other threads
remain in the records. Both quiet gates, full goldens and zero final worker
budgets pass. Workers/profilers join, owned fixtures are removed, service exits0,
MainPID0, empty control group, lease free. Finite refusal and real cancellation
remain separately qualified by existing runtime controls. The capture uses the
ordinary shared unbounded allowance, preserving admission/ownership operations.
Ongoing tiering, profiler/transport overhead and incomplete/inlined native frames
prevent a steady-state or latency decomposition. Do not subtract guest CPU from
wall time to call the remainder I/O or cleanup.

## Source decision

| Exact managed self category | Loop samples | CSV samples |
| --- | ---: | ---: |
| ExecuteUntilCall, both Tier1 forms | 211 | 34 |
| StoreLocalFromStack | 47 | 3 |
| ExecutableValueStack.Push | 37 | 3 |
| ExecuteOperandValueOperation | 27 | 6 |
| PyRange iterator | 44 | 0 |
| AddExactIntegers | 22 | 1 |
| MapExecutableAugmented | 9 | 0 |

The main method includes inlined guards/handlers; its211 samples cannot all be
called dispatch overhead or removable CPU. Boxing/allocators can be inlined or
unresolved, so small named arithmetic leaves do not prove boxing is free. This
pair does **not** justify a universal scalar representation or new integer backend.

A compile-only reflection inspection of the same frozen core proves the actual
four-operation sequences in [ir-proof.json](ir-proof.json):
`LoadLocal(total), LoadLocal(i), Augmented, StoreLocal(total)` and
`LoadLocal(accepted), LoadConst(1), Augmented, StoreLocal(accepted)`.
Existing source pushes both operands, computes, replaces/pops stack values, then
performs a separate local store. Range iteration already has cancellation checks;
integer operations already use native BigInteger helpers. No duplicate arithmetic
implementation or unconditional removal of checks follows from these samples.

Select one bounded design assessment/prototype: consolidate eligible same-block
local updates inside the existing executable engine, reusing its operation helpers
and local-store policy. Remove three VM dispatches and pure-integer operand round
trips; preserve actual operands through dynamic callbacks/suspension and maintain
an isolated CLR-temporary lifetime. PLAN.md freezes exact shape/lifetime, semantic
acceptance and a controlled material-gain comparison before code. This is a source
hypothesis about whole update transport, not a predicted gain or allocator share.

## Evidence and setup corrections

The initial launch runs as root and fails resolving /root paths **before any
worker or profiler starts**. Its original gate/reference/error/terminal records
remain under setup-failure in the raw archive. Anchor paths to the collector file
and launch as lokad; workload/warmup/capture/noise criteria are unchanged. There is
one actual sampling pair. Initial non-root tar cannot read sudo perf.data; complete
read-access retrieval succeeds without executing workloads again. An initial
compile-only IR dumper reads a non-applicable tagged operand and is corrected to
inspect operands only after their opcode tag; guest code is never executed there.

[All thread deltas, identities, gates and decisions](summary.json), both complete
origin files and [raw capture](raw-capture.tar.gz), SHA256
`a04b26450f18bf8525aacea8cb7da193cf6e232be47f2d099c28c8d207f0ad12`,
preserve every response, map, event, setup failure and terminal result. One
malformed trailing map record per case remains visible. Offline origin derivation
uses saved events only. Current runtime documentation and the maintained external
CPython table are unchanged. Stop B71; no further capture is selected.
