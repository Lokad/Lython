# Exact-byte truth reduction: short diagnostic

Exact-byte any/all now read byte!=0 directly with the established64-pull policy.
This removes integer boxing/iterator/truth transport for bytes; ranges/general
sources remain unchanged. This compares Lython with earlier Lython, with no
CPython timing baseline. The primary account stays in [BENCHMARKS.md](../../../BENCHMARKS.md).

| Warm all(values) | Values/sample | Before median | After median | Before/after spread | Allocated bytes before/after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Bytes, synchronous | 4,096 | 0.3256 ms | 0.00965 ms | 13.2% / 41.5% | 131,600 / 368 |
| Bytes, RunAsync | 4,096 | 0.32095 ms | 0.0124 ms | 12.5% / 87.1% | 131,688 / 456 |
| Range, synchronous control | 4,096 | 0.4201 ms | 0.3827 ms | 11.3% / 16.7% | 131,640 / 131,640 |
| Range, RunAsync control | 4,096 | 0.39805 ms | 0.4065 ms | 3.6% / 5.7% | 131,728 / 131,728 |
| General list, synchronous control | 128 | 0.0115 ms | 0.01255 ms | 19.1% / 35.9% | 520 / 520 |
| General list, RunAsync control | 128 | 0.0221 ms | 0.01965 ms | 25.3% / 27.5% | 680 / 680 |

Spread is (maximum-minimum)/median across eight samples. Byte allocation drops
by over99.6%; controls retain exact allocation. Time drops sharply in this single
unqualified local capture, with large relative candidate spread at microsecond
scale. No qualified elapsed/RSS/embedding-latency/current-HEAD CPython multiplier
follows. Lists are smaller/preboxed and are not cross-row performance baselines.
The witness times all; any shares the predicate kernel but has no separate timing
claim. Range boxing is deliberately unchanged.

Measured2026-10-17 UTC, Intel Core i7-14700KF, Windows10.0.26200 x64, Release
net10.0. Baseline a35ee62c; candidate is its one-file byte predicate patch before
commit. [diagnostic.json](diagnostic.json) pins source/body/recipe hashes and every
original tick/byte sample. These are historical source identities.

The [temporary recipe](ByteTruthReductionProbe.cs.txt) reuses the native-any/all
capture:compile return all(values) once, prepare context/inputs outside counters,
warm four times, take eight current-thread/Stopwatch samples. Bytes are1; range
is1..4096; list contains preboxed1. Both APIs must complete inline. The same16MiB
shared root and ordinary protections apply; outputs/unwind/refunds asserted.
No actual I/O, forced GC, custom tiering or protection switches. Separate tests
cover exact fuel/first refusal, aliases, guest I/O/cancellation and complete
binary-file output. No new scratch, owner, copied storage or iterator framework.

Reproduce by temporarily copying the recipe into the white-box project, building
Release and selecting only ByteTruthReductionProbe. Archive and remove before
final qualification; it is absent from the permanent suite. Stop this short
capture; no long lane or open-ended qualification campaign is implied.
