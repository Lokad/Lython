# Native truth entry: short diagnostic

Native truth now bypasses async state-machine entry and reuses PyTruthiness.
Guest slots retain the same awaited body. This compares Lython with earlier
Lython, with no CPython timing baseline. Allocations are unchanged and elapsed
benefit is unqualified. Primary CPython timings stay in [BENCHMARKS.md](../../../BENCHMARKS.md).

| Warm all(values) | Values/sample | Before median | After median | Before/after spread | Allocated bytes before/after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Native, RunAsync | 4,096 | 0.4489 ms | 0.4141 ms | 2.7% / 19.3% | 680 / 680 |
| Native, synchronous control | 4,096 | 0.16245 ms | 0.16365 ms | 6.9% / 17.0% | 520 / 520 |
| Guest, synchronous control | 128 | 0.12105 ms | 0.1225 ms | 21.1% / 45.3% | 81,416 / 81,416 |
| Guest, RunAsync control | 128 | 0.20285 ms | 0.20085 ms | 16.1% / 23.4% | 81,576 / 81,576 |

Spread is (maximum-minimum)/median across eight samples. A 7.8% lower native
RunAsync median alongside19.3% candidate spread establishes no speedup. No RSS,
embedding-latency or current-HEAD CPython multiplier is implied. Native and guest
row sizes differ and must not be used to compare per-operation performance.

Measured2026-10-17 UTC on Intel Core i7-14700KF, Windows10.0.26200 x64, Release
net10.0. Baseline9133e9ed; candidate is the one-file native-entry patch on that
revision before commit. [diagnostic.json](diagnostic.json) pins source/recipe/guest
body hashes and all original ticks/bytes. These historical identities do not
represent an arbitrary later HEAD.

The [temporary recipe](NativeTruthEntryProbe.cs.txt) compiles return all(values)
once, constructs context/list/guest objects outside measurement, warms four times
and records eight samples. Native values are True; guest objects return True
through __bool__. Both APIs must complete inline. Stopwatch ticks and current-
thread allocations cover execution only; outputs/unwind/refunds are asserted.
A16MiB shared root is explicit for both sources. No forced GC, custom tiering,
protection switches or real I/O. Separately qualified tests cover actual I/O,
cancellation, terminal refusal, retained aliases, exact fuel and complete CSV output.

Reproduce by temporarily copying the recipe into the white-box project, building
Release and running only NativeTruthEntryProbe. Archive and remove before final
qualification; it is absent from the permanent suite. Stop this short diagnostic;
do not turn its spread into an open-ended timing campaign.
