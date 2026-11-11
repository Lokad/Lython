# R220 — admission-lock callers from existing text stacks

Offline analysis, 2026-11-11. Reuses the exact R211 qualified `e543bf15` /
`0e2bc55b` capture on the reference F4as_v7 VM. No new capture, elapsed timing,
runtime change, producer qualification or current-HEAD CPU distribution is claimed.

All **64 Monitor leaves among 1,151 guest-thread samples** have an immediate,
uniquely mapped Lython caller. There are 33 Enter and 31 Exit leaves:

| Immediate caller | Samples |
| --- | ---: |
| `MemoryGovernor.Reserve` | 59 |
| Aggregate cased-whitespace preparation | 3 |
| `PyString.CopyFromUtf8` | 1 |
| `PyString.AllocateGovernedUtf8` | 1 |

Complete chains associate 38 with aggregate split admission and 26 with UTF-8
line/string construction. Inlining accounts for the different immediate callers.
The four relevant source files remain byte-identical after newline normalization
from `e543bf15` through the recorded `0b8f9e9a` source. [Proof](source-proof.json),
[all caller addresses, candidates and paths](caller-attribution.json).

These observations identify real admission paths, not lock contention, redundant
resizes, a removable CPU fraction or a speedup forecast. `Commit` already avoids a
host resize; ordinary `Reserve` reads reservation Bytes only for reentry/failure.
No new allowance/grant/counter policy is selected. Earlier coarse-grant adversity
remains separate. The next bounded call/frame hypothesis concerns incoming lexical
writes, whose unnecessary context promotion is visible in the current source.

The initial map guard stopped on duplicate starts before attribution. The saved
map contains 145 duplicate-start groups, including reused dynamic stubs. The
revised lookup considers every containing interval, deduplicates exact rows and
retains ambiguous aliases explicitly. **No ambiguous mapped frame affects these
64 paths.** Native/unmapped frames elsewhere, inlining and unadjusted caller return
addresses remain limitations. No original R211 leaf or raw record was overwritten.
[Decision](decision.json), [initial guard](initial-map-guard.py).

Run `python -X utf8 analyze.py` here to reproduce `recomputed-callers.json` directly
from the [canonical R211 archive](../2026-11-10-current-text-construction-cost/observations.tgz).
All computed fields were independently reproduced against that archive. The
original raw stacks/maps and immutable source/collection conditions remain in
[R211](../2026-11-10-current-text-construction-cost/README.md); they are not duplicated.
No runtime checks need replaying for this offline analysis.
