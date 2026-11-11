# R235 — captured operand transport, withdrawn

2026-11-11. **Both prototypes are fully removed. All568 production files match
qualified15f457f7 byte-for-byte.** Keep independent error-span and captured-input
lifetime regressions; assertions specific to the discarded encoding are removed.
No new reader, source bank, nullable-span request, deferred-context guard, dictionary
lookahead guard, opcode, pool, cache or alternative engine remains.

The proposal forwards adjacent captured reads through the existing direct operand
reader, eliminating LoadClosure publication/retirement. Constants/locals already
forward. Five production files eventually change. Native-return proof stays in its
separate pre-forwarding domain. Missing-cell fallback needs the actual invocation.
This adds shared read-bank discrimination and fallback coupling, with no runtime
allocation saving. [Source/image identities](diagnostic-identity.json).

The initial source eagerly looks up original error spans even for successful cell
reads. A7s two-image instrumented JIT check finds no inlining loss: both inline two
read sites/call one. Reader265→472B/input handler3671→4161B in those processes;
sizes are not removable CPU or portable offsets. Refine the existing closure reader
to look up forwarded spans only on failure. [Refinement decision](refinement-decision.json).
All initial source/cores/JIT output/measurements remain preserved.

Initial and refined candidates each pass530 focused checks across actual.NET11/10,
with fresh builds22.74/21.99s and zero warnings/errors. Six deleted-capture cases
pass before the change too. Local fixed9a4272de images differ only in core; same
six maintained complete scripts, normal GC/tiering,256warmups/image and5rotated
16-invocation rounds with two identical baseline ALCs. Runtime allocation is flat;
compile allocation−0.02..−0.06% on affected scripts. Refined captured-CSV async
elapsed−6.25..−6.31% with0.07% baseline spread, but other controls move up to16.82%.
These remain diagnostic observations. [Initial](initial/warm-async/summary.json),
[refined sync](warm-sync/summary.json),[async](warm-async/summary.json),
[separate compilation](compile/summary.json) retain every sample and wider CPU.

One bounded quiet confirmation follows the demonstrated source correction:

| Complete script / API | Quiet elapsed change versus15f | Wider process CPU change | Reference / candidate spread |
| --- | --- | --- | --- |
| Captured-counter CSV / sync | +0.07..+3.01% | −3.23..+3.12% | 2.15% /0.72% |
| Captured-counter CSV / async | +0.61..+5.72% | 0.00..+7.14% | 1.22% /3.79% |
| Captured-counter text / async | −3.08..+8.26% | +2.88..+8.77% | 0.07% /10.42% |
| Ordinary text / async | +0.39..+6.05% | −4.80..+10.13% | 1.17% /4.41% |

Ranges span all four process-median pairings, not confidence intervals. Four
independent workers/job,5swarmups/7rotated approximately150ms batches. Normal
GC/tiering, separately fixedfec79 worker/adapter; only core differs within each
quiet pair. Existing F4as_v7/Ubuntu24.04.4/.NET11RC3. [Quiet summary](quiet/summary.json).
**No overall benefit is established.** Captured text is unstable; uncertainty is
not a failed/permanently rejected design. Withdraw this limited design because it
adds shared complexity without allocation savings or a convincing whole-script
benefit. A broader transport redesign may revisit; no fixed percentage veto.

Quiet warm includes execution, mediated RAM I/O/full checks/prior-result cleanup;
excludes compile/startup/fixture reset/protocol/final caches. Local envelope also
includes fixture reset/result disposal and has separate Run-only clocks. CPU is
coarse/wider than guest execution. No CPython ratio, pending-I/O/RSS/tail claim.
113.924s; all hashes/goldens/balances/quiet/join/RAM/archive gates pass.
180sowner/30stop/30margin, terminal cached/never requery. Final free262983680B.
Fresh canonical/all-alias proof retires obsolete9dadd, B573/B574 compressed copies
and B572 map; those paths are now off limits. Current15f/9ee7/c271 survive.
[Raw quiet source/cores/observations](quiet/observations.tgz),
[terminal](quiet/terminal-status.json),[inventory](quiet/inventory.json).

Restore all five production files; force nonincremental Release build23.22s/zero
warnings/errors. Restored107internal/158public per actual target pass (530total).
Retained tests cover original spans, suspended captured receiver/key/value aliases,
cancellation, terminal refusal and cleanup. [Checks](restored-checks.json),
[all568 restored hashes](production-restored.json),[decision](decision.json).
Reuse exact15f's cached full Windows/Ubuntu, actual11/10, originalStress and installed
consumer qualification; new-test platform coverage batches at the next delivery.
One restored-harness shell quoting error occurs after the successful build and
before tests; a literal Python source file corrects it without rebuilding/retiming.

Native word-storage admission was also inspected: current preparation already
reserves all eventual fragments once before the first loop effect, and fragment
creation uses reserved storage. No new word-batch mechanism is justified.
[All original/refined source, binaries, checks and JIT](preparation.tar.xz),
[restoration](restoration.tar.xz),[archive identities](archive-identity.json).
Whole-script objective remains open; publication postponed.
