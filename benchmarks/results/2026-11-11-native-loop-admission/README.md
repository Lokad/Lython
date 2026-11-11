# Native loop admission — withdrawn, 2026-11-11

The prototype retained one existing managed stack-admission scope across a native
dictionary-counting segment, replacing per-word entry/exit and its inner cleanup.
It passed 3,046 focused checks across actual .NET 11 and 10. It is **withdrawn**;
production source is restored exactly to qualified `8f8628a4`. No public API or
runtime contract change remains.

| Complete workload / API | Quiet elapsed change | Wider process CPU change | Reference control spread |
| --- | ---: | ---: | ---: |
| Captured text / Run | −1.17% to +1.78% | −2.27% to +3.45% | 2.60% |
| Captured text / RunAsync | −3.02% to +2.80% | −7.95% to +3.61% | 5.38% |
| Ordinary text / Run | −1.78% to −0.44% | −7.80% to +0.85% | 0.20% |
| Ordinary text / RunAsync | −5.05% to +1.28% | −5.22% to +0.88% | 5.36% |
| Unaffected CSV control / Run | −2.00% to −0.58% | −2.94% to 0.00% | 0.67% |

Change is candidate/reference − 1; each range includes all pairings of two
independent candidate and two reference processes. Wider CPU includes protocol,
reset, validation, disposal, JIT and GC. The ordinary text and unaffected CSV
observations do not establish a general CPU improvement. Desktop runtime and
compilation allocation are flat. Desktop captured RunAsync elapsed **+7.48% to
+8.35%**, with a 0.81% unchanged control, remains unexplained and preserved.

The small mechanical reduction adds cleanup state and holds an extra logical
nesting level across namespace-binding reentry, potentially rejecting nested work
earlier. The complete-script evidence does not justify that lifetime tradeoff.
This is a decision on this implementation, not a fixed percentage veto or a
permanent rejection of admission/call redesigns. No further unchanged retiming
is planned.

The existing Ubuntu F4as_v7 VM, .NET 11 RC3, normal GC/tiering, fixed `fec79`
worker, optional adapter 0.4 and unchanged scripts ran five jobs in **141.217 s**.
Warm timing includes mediated RAM-file I/O, output/file checks and prior-result
cleanup. Compilation is measured separately on the desktop; startup is excluded.
This is a Lython source-pair comparison, not a refreshed CPython baseline.
All identity, output, ownership, quiet, joined-user, RAM cleanup and independent
archive verification gates pass. B592 is terminal/cached; no users remain.

An initial disk inventory refused staging before mutation. The original failure
survives; reserve requirements were unchanged. Only exact duplicate evidence and
obsolete core aliases with committed canonical bytes and fresh path/UID/inode/
all-alias proofs were retired. Current selected references were protected.

[Decision](decision.json), [source/core identity](diagnostic-identity.json),
[exact patch](prototype.patch), [before source](sources-before.tar.xz),
[candidate source](sources-candidate.tar.xz), [focused checks](focused-checks.tar.xz),
[desktop Run](warm-sync/summary.json), [desktop RunAsync](warm-async/summary.json),
[compilation](compile/summary.json), [quiet summary](quiet/summary.json),
[raw observations/core](quiet/observations.tgz), [receipt](quiet/archive-receipt.json)
and [terminal status](quiet/terminal-status.json) retain the evidence. Reference
images are [linked canonically](reference-images.json), avoiding another bundle.
The prototype did not receive full platform/stress/package qualification. The
restored runtime's previous complete qualification remains applicable; publication
is postponed.

After restoration, [nonincremental Release builds](restored-builds.json) pass for
actual .NET 11 and 10. No stale prototype output is used as a restored binary and
no unchanged full platform/package qualification is repeated.
