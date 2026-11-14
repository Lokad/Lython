# Private cased-word inventory removal — withdrawn

2026-11-14, R348: qualified `009d236a` / frozen `8ae1ae5f` versus prototype
`4311a053`. The first form admits a worst-case input-length bound, eliminating
the preparatory scan and inventory. It rejects a20,002B/two-word input under1MiB
(1,360,136B output estimate):7failed/43passed focused checks. This is an excessive
working-set tradeoff, not an observed containment escape. Original source/core
and failures survive; budgets and semantic assertions were not relaxed.

A bounded refinement deletes the coarse estimate/remainder/refund machinery.
It preflights exact eventual word capacities with the existing scanner, then
rescans immutable input during consumption. Private range writes/growth/copy/
clearing and count/index state disappear; ordinary split inventories retain real
callers.127internal/162public per actual .NET11/10 pass (578total), including
Unicode, admission before effects, cancellation, actual suspension, retained
aliases, cache shutdown and cleanup. Allocation is flat, about+8B/text job.

| Complete warm script | Elapsed candidate/reference change | Wider CPU change |
| --- | ---: | ---: |
| Captured text counter,3,072lines /35,840B | +3.39..+11.19% | +2.86..+10.67% |
| Same input, direct text loop | +7.97..+17.52% | +7.59..+17.78% |
| CSV fulfillment,4,096records /126,821B, control | −0.86..+1.43% | −0.99..+0.89% |

Ranges compare every fresh-process median pairing; positive means more/slower.
Captured process spreads are3.65%candidate/3.75%reference; direct3.99/4.31%,
control1.96/0.35%. Magnitude is diagnostic, but every affected pairing is adverse
with CPU support and no useful allocation reduction. Withdraw the refinement;
stored ranges are faster here. No unchanged retiming is required for this decision,
and it does not prohibit a materially different pipeline redesign.

B719 uses the quiet F4as_v7/Ubuntu24.04.4/.NET11RC3 VM, ordinary GC/tiering,
unchanged B685worker/optional0.4adapter and identical complete scripts/goldens.
Four fresh processes/job,12s complete-request warmup, five1s batches. Warm clocks
include mediated RAM I/O, output validation and prior-result cleanup; exclude
compilation/startup, reset/protocol and final cache shutdown. Wider CPU includes
reset/protocol. No CPython timing, heap peak or embedding-tail claim.
224.83s stays inside the300s owner; all image/output/budget/quiet/join/archive
gates pass, no workers or RAM fixtures remain. Raw SHA256:
`debba5afe1dca14637ce65991a6f324e2c84ea1a5cc2ffad3c1cea9d5d71358b`.

B718 was stopped and explicitly restarted after60s despite300sRuntimeMax,
Restart=no/NRestarts=0. Origin is unresolved. The restart refuses an existing
confirmation directory; no complete jobs supply comparison evidence. Configuration,
journal, partial data and recovery are preserved. Users join and the remaining
RAM mount is archived/verified before unmount. B719 is the justified fresh same-bit
measurement correction; the original uncertainty remains visible.

Before staging, only five aliases to superseded830core bytes and two superseded
catalog copies were retired after canonical Git/SHA/path/UID/all-alias proof.
Current images/evidence/floors stayed protected. Initial missing-root preflight
failed before deletion and is retained. No broader storage campaign or floor change.

All581production inputs are restored exactly to qualified009d. Forced
nonincremental Release rebuilds precede35focused checks per actual11/10, all pass.
The useful retirement regression remains: end idle reuse before arming an actual
refund callback, independent of buffer capacity. Its retry/alias/cleanup assertions
stay intact and also pass the exact frozen8ae baseline. No new runtime mechanism
remains and no unchanged full platform/package qualification is repeated.
The original full source qualification is [43563853239](../2026-11-14-indexed-sequence-cursors/delivery/README.md);
the test-only fixture adjustment remains focused-qualified pending a later batch.

[Decision](decision.json), [timing](timing-summary.json),
[complete source/core/tools/checks/failed conditions](prototypes-checks-and-observations.tar.xz).
Publication stays held; substantial complete-script work remains active.
