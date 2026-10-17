# Native any/all: short diagnostic

Exact bytes/ranges now use existing synchronous any/all kernels under RunAsync.
General iterables retain unchanged awaited loops. This compares Lython with
earlier Lython and has no CPython timing baseline. The primary external account
stays in [BENCHMARKS.md](../../../BENCHMARKS.md).

| Warm all(values) | Values/sample | Before median | After median | Before/after spread | Allocated bytes before/after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Bytes, RunAsync | 4,096 | 0.89825 ms | 0.27925 ms | 7.1% / 19.3% | 131,824 / 131,688 |
| Range, RunAsync | 4,096 | 2.0352 ms | 0.42105 ms | 250.4% / 6.8% | 131,824 / 131,728 |
| Bytes, synchronous control | 4,096 | 0.3302 ms | 0.33115 ms | 8.4% / 8.2% | 131,600 / 131,600 |
| Range, synchronous control | 4,096 | 0.4045 ms | 0.4141 ms | 70.3% / 9.1% | 131,640 / 131,640 |
| General list, synchronous control | 128 | 0.01455 ms | 0.0121 ms | 29.6% / 32.2% | 520 / 520 |
| General list, RunAsync control | 128 | 0.02605 ms | 0.02465 ms | 35.7% / 25.6% | 680 / 680 |

Spread is (maximum-minimum)/median across eight samples. Bytes show a68.9% lower
RunAsync median in this single unqualified capture. Range timing is inconclusive
because baseline spread exceeds the median change. No qualified speedup, RSS,
embedding-latency or current-HEAD CPython multiplier follows. List inputs are
smaller and preboxed; cross-row times/allocations are not per-operation comparisons.
About128KiB still allocate for4096 native values; this change retains integer
boxing and existing iteration. It does not qualify a scalar representation change.

Measured2026-10-17 UTC, Intel Core i7-14700KF, Windows10.0.26200 x64, Release
net10.0. Baseline c62b5e19; candidate is its one-file native-routing patch before
commit. [diagnostic.json](diagnostic.json) pins candidate/kernel/recipe hashes and
all original ticks/bytes. These are historical source identities.

The [temporary recipe](NativeAnyAllProbe.cs.txt) compiles return all(values) once,
prepares context/bytes/range/list outside measurement, warms four times and takes
eight current-thread allocation/Stopwatch samples. Native bytes are1; the range
is1..4096; general list inputs are preboxed1 values. Both APIs must complete
inline. A16MiB shared root and normal protections apply; results/unwind/refunds
are asserted. No real I/O, forced GC, custom tiering or protection switches.
Separate tests cover exact fuel/refusal, aliases, guest iterator I/O/cancellation
and complete binary-file output. This witness measures all; any shares routing
but has no separate elapsed claim.

Reproduce by temporarily copying the recipe into the white-box project, building
Release and running only NativeAnyAllProbe; archive and remove before final
qualification. It is absent from the permanent suite. Stop this short capture;
no long lane or open-ended qualification campaign is implied.
