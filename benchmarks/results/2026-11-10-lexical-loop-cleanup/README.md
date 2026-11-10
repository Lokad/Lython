# Lexical loop cleanup — R207

2026-11-10, qualified runtime `dba779a5` / immutable `101ba9d3` before versus
focused-tested `dbe94d4d`. Exactly two production compiler files differ. Candidate
comes from a forced nonincremental Release solution build; all auxiliaries and
existing scripts/fixtures/goldens are unchanged. Full source hashes and the original
baseline forced-freeze proof remain in preparation evidence. No new regex producer.

The compiler previously emitted abrupt routing whenever any try/with was active,
even if the loop and transfer remained inside that same suite. Loop metadata now
retains the existing lexical protected depth. Same-depth break/continue use ordinary
Jump/iterator-pop; actual boundary exits keep pending transfers and cleanup routing.
This removes per-transfer PendingJump construction, region scans and handler unwind
work on the normal within-suite path. No new runtime state, dispatcher, policy,
owner, cache, scalar representation or public API. Runtime pending machinery remains
necessary for genuine cleanup exits. All instructions still check active cancellation.

R206's PendingJump association selected this source assessment; it was not exact
allocation by type or CPU dominance. Complete validated CSV now allocates about
2.47% less on both APIs. Other complete-script allocation controls are essentially
flat. Compilation allocation changes about −0.07% CSV / +0.01% controls. These
are CLR allocations, not host reservation estimates, heap/RSS or latency evidence.

Original desktop elapsed remains adverse/unstable: CSV +2.24..9.29% sync and
+7.55..14.93% async; text/config controls also move adversely. JSON sync is favorable,
async controls mixed. Compile elapsed is inconclusive. No desktop CPU gain or
regression size is inferred; every phase/sample/control remains [raw](desktop/).

B558 completes three jobs in 110.421 s, quiet F4as_v7 Ubuntu24.04.4/.NET11RC3,
normal GC/tiering, four independent roles, 5-second warmup each and seven rotated
400 ms rounds. All identity/output/ownership/quiet/Closed/join/RAM/archive-under-lease
and local SHA checks pass. No workers or private RAM fixtures remain.

| Complete script / API | Elapsed change | Wider process CPU change | Control spread |
| --- | ---: | ---: | --- |
| Validated CSV / sync | −2.73..+3.49% | −3.41..+3.49% | reference 3.74%, candidate 2.42% |
| Text helper control / sync | −5.65..+1.90% | −5.65..+2.87% | reference 4.36%, candidate 3.37% |
| Validated CSV / async | −1.45..+0.54% | −1.28..−0.89% | reference 0.28%, candidate 1.73% |

Ranges compare both candidates with both references, not confidence intervals.
Synchronous effects are inconclusive; async wider CPU offers modest support,
while elapsed straddles zero. **No general CPU improvement is established.**
Warm clocks include mediated RAM I/O, capture/projection and prior-result cleanup;
include full per-invocation output checks; compilation, fixture reset, separate
verification requests/protocol and final-cache teardown are excluded. Wider
CPU includes surrounds/coarse ticks. No startup, pending-I/O latency or new CPython
ratio. Historical CPython rows retain their exact sources.

Retain for actual work elimination and allocation reduction without extra runtime
machinery, with elapsed uncertainty/debt explicit. This is a complete compiler
classification, not a preparatory fact promoted alone. It does not close the broader
whole-script objective. Next construction gate assesses completed tuple values,
including real borrowed-array copies and native stack construction, rather than
reintroducing scalar sidecars, inlined tuple representations or another call arena.

Focused Release 54 internal / 254 public cases pass per actual .NET11/10;
solution build has zero warnings/errors. Three new CPython3.13.2 goldens preserve
pending return/exception and live handler alias/reraise/deletion through local loops.
Existing crossing managers/finally, nested loops, generator suspension, real delayed
cleanup, terminal admission, ownership reuse and new active non-allocating continue
cancellation pass. Full local ordinary suites also pass: 4,518 internal / 17,105 public per actual
target. Platform/stress/installed-package delivery remains pending; do not imply
the source already passed those gates.

Staging retires six exact still-live B557 non-core raw duplicates only after matching
committed archive bytes, UID/path/inode/all aliases under a fresh idle lease. Already
retired cores/archive and missing B546 raw are never probed. Protected039/E294/101
survive, with the unchanged 240MiB floor / 12MiB setup headroom. A local retrieval
request seven seconds early is refused before SSH; after the guard wait, the same
run is retrieved with no restart or measurement change. Original condition retained.

[Summary](summary.json), [decision](decision.json), [focused checks](focused-validation.json),
[raw cores/observations](observations.tgz), [archive receipt](archive-receipt.json),
[terminal status](terminal-status.json). Preparation, source patch/hashes, oracle
sources, build/test logs and original guard condition are in
[preparation evidence](preparation.tar.xz).
Publication and tagging remain postponed.

Retained439e91d9 subsequently passes full Windows/Ubuntu delivery: ordinary actual
NET11/10, original Stress and installed Release consumers. [Cached terminal proof](delivery-ci.json);
do not query completed42888642699 or repeat unchanged qualification.
