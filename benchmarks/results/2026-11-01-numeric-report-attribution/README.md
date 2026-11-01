# Complete numeric-report attribution (B52)

2026-11-01, production source95b5955c, informational20e208c0 after fresh source
restoration; production at5f3338a8 is unchanged. Equal fresh core/worker/test/probe
copies, SHA256`6e51564be11be5702d215c3e1022fc114832aa0ce1a706a9f699de294203df02`.
**Sparse capture; no value-representation rewrite or speedup claim.**

## Fixed pair and boundary

[Criteria before collection](pre-collection-plan.md):512 complete warm synchronous
jobs each for the maintained integer/float numeric-report scripts,4,096-item inputs.
Both load JSON arrays, sum them and write a full JSON report. Dedicated Ubuntu24.04.4
Azure F4as_v7, four EPYC9V45 vCPUs/16GiB, pinned.NET11RC3, ordinary GC/tiering.
Existing checked worker and199Hz cpu-clock/fp/perf maps; compilation/startup
excluded, real mediated I/O/full stdout/file validation/declared cleanup included.
One pair only; no recapture after sparse results.

| Case | Capture seconds | Actual guest CPU seconds | Guest samples | Unknown | Tiered-JIT CPU seconds |
| --- | ---: | ---: | ---: | ---: | ---: |
| Integer report | 1.02678 | 0.30 | 58 | 7 | 0.63 |
| Float report | 1.13641 | 0.38 | 75 | 8 | 0.66 |

Combined capture2.163s/owned service14.357s, below12s/45s bounds. All quiet/full
golden and worker/profiler-exit gates pass. Actual named execution threads identify
guest samples. All other threads, malformed map tails and unresolved frames remain
visible. Service inactive/dead/success and VM lease available. Overlapping JIT and
guest CPU cannot be subtracted from elapsed time to infer I/O or cleanup latency.

## Source finding and bounded decision

SumState already keeps native integer/float prefixes in scalar fields, with exact
overflow promotion and Python compensated-float behavior. Ordinary JSON integers
and floats already parse from UTF-8; only large integers retain text fallback.
Those facts exclude obsolete boxing/conversion proposals. Shared immutable builtin
definitions also already exist; mutable type/exception graphs need run isolation.

Observed JSON value/array reading is prominent in both small traces; native float
parsing is also visible. Counts are too sparse to establish a dominant removable
CPU fraction. They do not justify a universal value/frame rewrite or another local
number-scanner/binder/reservation optimization. No numeric candidate is selected.

A separate concrete source opportunity is recursive container transport and
duplicated object grammar: ReadJsonObjectAsync has separate pairs-hook/dictionary
loops; ReadJsonArrayAsync and both object loops await ReadJsonValueAsync for every
child. Test replacing them with ONE iterative parser and an admitted private stack
of unfinished containers. Preserve the existing scalar/string scanners, Python
errors/positions/hooks and real callback suspension; remove the original readers,
not add a fast default parser or retry/fallback. This is an algorithmic source
hypothesis, not a speedup inferred from58/75 samples. Freeze its deletion, ownership
tradeoff and controlled materiality gate in PLAN.md before any candidate. Stop B52.

[Full summary](summary.json), [source identity](source-identity.json), readable
collectors/analyzers and [raw capture](raw-capture.tar.gz), SHA256
`10961f0dc62551e0ff63e9c8c1be93ab5bc30eb2496513af36cceada7bea1657`,
preserve every sample, chain, input, thread delta and machine/golden gate.
