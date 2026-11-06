# Development adapter: native cursor ranges — 2026-11-06

The selected **private** dev40 adapter uses native capture byte ranges and keeps
the existing current-token bridge. The normal optional adapter remains pinned to
0.4.0; core source/dependencies, public APIs and publication are unchanged.
This continues development independently of a published producer release.

Both prototypes pass **84 internal / 421 public cases per actual .NET 11/.NET 10**,
including real in-flight compile/match/cursor cancellation, cancelling an earlier
Find token before a later advance, captures/empty progression, frontend/runtime/
imports/fnmatch/glob semantics, refusal, retained storage and budget reuse. Exact
package, test and worker-fixture DLL hashes are recorded. Initial duplicate-source
build errors and five missing relocated workflow fixtures are preserved; fixing
the private harness changed no assertion. Only those five .NET11 failures were
replayed; unchanged successes were reused. The range-only source changed again,
so both-runtime focused checks were run for that variant.

| Same dev40 producer, direct cursor diagnostic | Allocated bytes per cursor, two roles | Change vs original adapter |
| --- | ---: | ---: |
| Original adapter source | 28,976 / 28,976 | baseline |
| Direct advance token + native ranges | 29,640 / 29,640 | **+2.29%** |
| Selected native ranges + existing token bridge | 22,368 / 22,368 | **−22.81%** |

This measures exact synchronous current-thread allocations for creation, 64
advances/conversions and disposal over a 192-byte UTF-8 subject, with named,
unmatched and successful empty captures. Compilation/preparation/startup are
outside the boundary. Full byte positions, LastIndex, checksums and allowance
baseline/zero cleanup pass. 32 warmups, seven batches of 128 cursors, four processes
in baseline/candidate/candidate/baseline order; loaded core/backend hashes match.
Core runtime source 88f12099 plus receipt f145daa3, .NET 11 RC3, Windows. The earlier
standalone diagnostic pattern is reused; no production instrumentation/forced GC.

The adverse direct-token result has a concrete source cause: exact producer
6f6fdab9's Utf8PythonMatchCursor.MoveNextCore links each advance token with its
cancellable creation token. Disposing the adapter's synthetic creation CTS does
not make that token noncancellable. Removing the adapter field therefore moved
linked-token work into every producer advance. Creation validates all input;
using None with entry/exit checks would lose real in-flight cancellation.

The bounded refinement retains the existing adapter-local bridge and replaces
only detailed Current snapshots with native ranges. It removes a conversion
boundary without adding a runner, owner, quota or configuration. Both source
pairs/results remain separate. A producer API separating cancellation during
creation from future advances remains necessary for the intended token-bridge
simplification. That gap does not block core work or contract correctness.

Busy Windows elapsed medians are retained in both raw diagnostics (~0.103–0.148ms
per cursor) and are **not qualified CPU/latency improvements**. The 22.81% reduction
is cursor-only, relative to original adapter source on the same development
producer; it is not a whole-script allocation figure, CPython ratio, retained-heap,
RSS or embedding-process result. No broad benchmark campaign or VM rerun.

Exact immutable dev40 producer source 6f6fdab9: Core package eb5d3ad4 and PythonRe
package 31c8d9f2, full identities in source-assessment.json. Development source and
patches accompany this evidence; the isolated props/feed/outputs leave the normal
pin/lockfiles untouched. This is focused development qualification, not producer
managed-safety assessment, exact backend working/cache receipt integration or
released-adapter promotion. One-shot/name/error/window bridges and final producer
capability checks remain independent work. Publication is postponed.
