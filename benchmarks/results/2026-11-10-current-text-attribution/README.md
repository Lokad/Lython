# R199 — current text CPU attribution and source gate

2026-11-10, committed66ebdb4b production source matches frozen039dc975; existing
complete text-summary/helper script, exact fixtures/output goldens and worker,
optional adapter0.4. F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering plus
diagnostic perf-map logging. Full source CI42822411897 was pending at capture.
No new source optimization, elapsed comparison, CPython timing or compilation.

One warmed capture completes with1,144 execution-thread samples,71unmapped6.21%,
5.76threadCPU seconds. Worker execution clocks sum5.855seconds; the six-second
wall window also includes reset/protocol/validation. [All leaves/thread CPU](attribution.json),
[raw maps/stacks/record/core](observations.tgz), [receipt](archive-receipt.json),
[preparation/source gate](preparation.tar.xz). The actual CPU-active execution
thread is selected, never assumed to be process main. No malformed map lines.

| Innermost observed group | Samples | Share |
| --- | ---: | ---: |
| Other mapped leaves | 290 | 25.35% |
| VM instruction prefix | 224 | 19.58% |
| Whitespace/casing | 174 | 15.21% |
| Native dictionary/hash | 147 | 12.85% |
| Native call selection | 80 | 6.99% |
| Unmapped | 71 | 6.21% |
| Operand/result transport | 67 | 5.86% |
| Monitor locking; caller not attributed | 58 | 5.07% |
| Scalar arithmetic | 33 | 2.88% |

These disjoint groups include useful and inlined work, unknowns and the wider
worker boundary. They are not removable taxes or speedup forecasts. Monitor
samples have no proved caller attribution; do not automatically assign them to
memory admission. Binding/frame construction is not a prominent observed leaf.

Bounded source findings: whitespace range collection already computes case
capacity in the same scan; final words write direct UTF-8. Native string hashing
already takes the direct current-token path, not the general comparer/protocol
route. MemoryGovernor.Commit changes accounting categories without resizing the
shared lease. Do not recreate those optimizations or weaken admission/cancellation.
There is no demonstrated dominant duplicate frontend pass in this warm capture.

Next source assessment: reduce the hot instruction loop's cold operation body and
live temporaries while retaining the one executable engine. Current prefix/operand
work is material; R194's larger OSR frame and slower variants argue for examining
code/frame size before another control-loop change. This is a hypothesis, not a
predicted gain or selected replacement implementation. Define displaced cold cases,
semantic gates and a bounded complete-script comparison before editing. Broader
performance and independent regex work remain active; no unchanged recapture.

Verification before/after, normal close/join, quiet gates, temporary-map copy/SHA
before retirement, empty RAM unmount, persistent archive-under-lease and localSHA
verification all pass. No active VM users or RAM traces. No publication or adapter
pin change. No unchanged full-platform qualification is repeated for this source-only
attribution; pending qualification remains tracked separately.
