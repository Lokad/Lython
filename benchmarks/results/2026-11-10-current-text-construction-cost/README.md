# R211 — complete-text construction cost gate

2026-11-10, fully qualified e543bf15 production source, exact fresh postcommit core
0e2bc55bac5ebfafd9d30014e34e2afb3e9776bbe6b5d83a53cc710c0d99c492, all 568 source
hashes unchanged. Existing text-summary/helper script, fixtures, full goldens,
worker and CPU collector. Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering
plus diagnostic perf-map logging. R199 predates material split/loop/tuple changes;
this is one current attribution, not an unchanged elapsed retiming.

One warmed six-second wall capture: **1,151 execution-thread samples**, 68 unmapped
(5.91%), 5.79 execution-thread CPU seconds and 5.888 summed worker execution seconds.
Actual CPU-active thread selected, not assumed to be process main. No malformed
map lines. [All leaves/threads](attribution.json), [raw core/maps/stacks/records](observations.tgz),
[identity](identity.json), [source proof](source-proof.json), [receipt](archive-receipt.json),
[preparation/original conditions/source](preparation.tar.xz).

| Innermost observed group | Samples | Share |
| --- | ---: | ---: |
| Other mapped leaves | 313 | 27.19% |
| VM instruction prefix | 235 | 20.42% |
| Whitespace/casing | 154 | 13.38% |
| Native dictionary/hash | 134 | 11.64% |
| Operand/result transport | 81 | 7.04% |
| Native call selection | 71 | 6.17% |
| Unmapped | 68 | 5.91% |
| Monitor locking; caller not attributed | 64 | 5.56% |
| Scalar arithmetic | 31 | 2.69% |

Groups use the same disjoint rules as R199. They contain useful/inlined work and
the wider reset/protocol/full-output-check boundary. Unknowns are retained. These
are neither removable taxes nor speedup forecasts. Monitor callers are unproved;
do not assign every lock sample to memory admission. This does not compare normal
elapsed performance, CPython, compilation, startup or embedding latency.

Split preparation has 105 leaf samples (9.12%). Source confirms its first scan
computes exact conservative aggregate admission for every eventual word before
any guest loop effect. Consumption then avoids the eager list/value/range arrays.
Replacing the first scan with worst-case per-byte word overhead would greatly
overcharge long tokens; restoring eager storage would reverse delivered R203.
Neither is promoted as a simplification. Short string bytes are already inline.
Calls use the existing native helper entry; complete frame construction is not a
demonstrated dominant cost here. Instructions already overlay operands and forward
locals; dictionary accumulation already shares one probe and string hashes cache.

The [source gate](source-gate.json) finds no justified complete migration from
these shares. Continue the frontend source gate using existing evidence, and the
independent regex track. Do not manufacture a local optimization or expand this
into a profile campaign. A genuinely different design can receive a bounded
confirmation when its eliminated work and semantic acceptance are concrete.

All source/fixture/output identities, verification before/after, quiet gates,
Closed/zero-exit/join, map copy/SHA, RAM removal, archive-under-lease and local SHA
pass. Terminal status is cached; no remaining workers or RAM traces. Staging retired
only two exact committed compressed duplicates (B558/B561), with fresh idle lease,
path/UID/single-link/hash proof and fixed 240 MiB floor / 12 MiB headroom intact.
An attempted retrieval 2.85 seconds early failed locally before SSH; after five
seconds the same owner was retrieved, with no restart or repeated capture.
No production change, redundant platform qualification, adapter promotion or publication.
