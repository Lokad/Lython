# Native dictionary-read dispatch diagnostic — 2026-10-17 UTC

All eight measured samples in every case allocate **320 bytes per 128 reads**,
before and after the guard change. Removing these completed async helpers does
not demonstrate an allocation reduction.

| Native instruction-runner case | Before median | Guard-only median | Allocation before / after |
| --- | ---: | ---: | ---: |
| Dictionary, synchronous execution | 0.06960 ms | 0.08365 ms | 320 / 320 bytes |
| Dictionary, asynchronous execution | 0.11270 ms | 0.07990 ms | 320 / 320 bytes |
| List control, synchronous execution | 0.06185 ms | 0.05840 ms | 320 / 320 bytes |
| List control, asynchronous execution | 0.06985 ms | 0.06940 ms | 320 / 320 bytes |

The asynchronous dictionary median is lower, while the unchanged synchronous
dictionary control is 20.2% higher. This local diagnostic establishes no qualified
elapsed improvement. The boundary regressions were also enabled in each process,
allowing concurrent-test interference; same-class diagnostic cases run sequentially.
No CPython, complete-job, compilation or startup performance was measured here.

The baseline is 2cb4a7863366cc48a1404ca46f624bf1fcfbdb31. The candidate is that
source plus the exact [guard patch](guard.patch.txt). This capture predates the
accompanying stored-key async correctness fix and synchronous-forwarder removal;
it is not a measurement of their final committed revision or today's HEAD.

The existing executable instruction runner performs 128 unrolled reads with
precompiled source, an initialized context and an existing native dictionary or
list. Construction and compilation are outside samples; instruction execution,
operand transfer and result observation are inside. Each result must equal 7;
frame cleanup and reservation refunds are checked. This bypasses public invocation
scheduling and host I/O. Four warmups precede eight samples for each of four cases,
using per-thread managed allocation deltas and Stopwatch timing. There are no
forced collections, runtime overrides or second capture selected for a better result.

Machine: active Windows 11 Business workstation, build 26200 x64, Intel Core
i7-14700KF, 20 cores / 28 logical processors; Release .NET SDK
10.0.300-preview.0.26177.108 and runtime 10.0.13, ordinary GC and tiering.
The owned processes took 1.203 and 1.164 seconds under the existing 28-second
bound plus one-second cleanup. All nine selected tests pass in each process.

[Baseline samples](baseline-diagnostics.json), [guard-only samples](candidate-diagnostics.json),
[source and assertions](DictionaryReadDispatchProbe.cs.txt), and
[source/binary identities and boundaries](measurement.json) preserve the evidence.
The temporary diagnostic source was removed from the test project before delivery.
The [authoritative CPython comparison](../../../BENCHMARKS.md) retains its named
historical measurements; this diagnostic supplies no new external ratio.
