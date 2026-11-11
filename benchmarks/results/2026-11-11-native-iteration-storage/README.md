# Native iteration/result storage — withdrawn, 2026-11-11

All **three measured forms are withdrawn**. Production source is restored exactly
to fully qualified `8f8628a4`. The prototype did not receive full platform/stress/
package delivery qualification. Useful public semantic/lifetime regressions remain;
all new general result-handoff/state machinery is removed. Publication is postponed.

The prototype replaces per-line private cased-word carriers and the old integer
position array with one execution-owned iteration-state backing. An inline private
result handoff carries the immutable source through native, captured, borrowed,
framed and protected returns. Observable results remain real Python lists. Word
capacity is still admitted before loop effects; no pool/cache, alternative engine
or ownership registry is added. The backing reserves 48 bytes per slot plus
metadata, conservatively exceeding its physical layout; this larger fixed charge
can reject smaller allowances earlier. Per-line carrier metadata charges disappear.

Each form passes **9,178 focused checks** across actual .NET 11/10. Eight independent
CPython 3.13.2 goldens are retained and their unchanged public assertions pass in
all three forms. New public regressions cover nested native/character sources and
early break, plus a withdrawn source retained during genuinely pending protected
return cleanup. Cancellation waits for actual cleanup; the shared budget returns
to baseline and supports subsequent executions. No public API change is intended.

| Complete workload | Run allocation | RunAsync allocation |
| --- | ---: | ---: |
| Captured text/helper counter | −26.29% | −26.26% |
| Ordinary text/helpers | −28.86% | −28.83% |

Compilation allocation is flat; controls change by at most about 0.02%. These are
three-image desktop observations with the existing worker/driver, normal GC and
tiering, 256 warmups and five rotating rounds of 16 complete jobs. Compilation,
warm Run and warm RunAsync remain separate. Allocation reduction is established
for these scripts; CPU improvement is a separate question.

The initial form carried the private result through ordinary recursive value
evaluation. Its desktop search Run was +7.64–9.02%, with a 1.29% unchanged control;
CSV, text and report adversity and opposing async observations also survive. The
refinement moves return continuations out of ordinary value recursion, retaining
all checks and return-exit order. Its desktop clocks remain mixed, with large
unchanged controls on several scripts. Neither form predicts a general speedup.

The refined form's existing quiet Ubuntu F4as_v7/.NET 11 RC3 comparison uses two
independent processes per source and the fixed `fec79` worker/optional adapter 0.4:

| Complete script / API | Elapsed change | Wider CPU change | Reference / candidate control |
| --- | ---: | ---: | ---: |
| Captured text / Run | +0.72% to +10.85% | −4.17% to +17.07% | 0.05% / 9.09% |
| Captured text / RunAsync | −1.89% to +2.16% | −5.88% to +5.04% | 1.64% / 2.45% |
| Ordinary text / Run | −5.01% to −3.96% | −5.65% to −4.07% | 0.27% / 0.82% |
| Ordinary text / RunAsync | −4.55% to −0.67% | −4.35% to +5.33% | 2.99% / 1.04% |
| Keyword-default search / Run | −4.50% to +5.64% | −3.36% to +8.74% | 3.62% / 6.20% |
| CSV / Run | −1.19% to +0.09% | 0.00% to +3.53% | 0.97% / 0.33% |
| JSON-to-Markdown / RunAsync | +0.75% to +6.66% | +4.26% to +6.38% | 1.70% / 3.94% |

Change = candidate/reference − 1; ranges include all four process-median pairings,
not confidence intervals. Wider CPU includes reset/protocol/validation/disposal,
JIT and GC. Warm clocks include mediated RAM I/O, output/file validation and
prior-result cleanup; startup/compilation are excluded. Ordinary Run has supporting
CPU improvement. Captured/search remain variable; **report CPU is adverse**.
Original desktop failures/conflicts are not explained away by this confirmation.

B593 finishes seven jobs in **196.604 s**, within its 240-second owner. All
identity, output/files, ownership, quiet, joined-user, RAM cleanup and persistent
archive gates pass. The retrieved archive is independently SHA-verified. Only
committed canonical duplicates/withdrawn core aliases were retired under fresh
lease/path/UID/inode/all-alias proofs; the selected reference stayed protected.
The terminal receipt is cached; no VM users or RAM fixtures remain.

The final form retires only live reference descriptors and resets numeric list/
tuple progress at acquisition, removing unnecessary reference-state writes. Its
desktop allocation reduction is unchanged; report Run elapsed +12.27–28.07%, with
a 12.34% control, and the other conflicting desktop clocks remain preserved.

Its five-case quiet confirmation, B594, finishes **142.232 s** within the
180-second owner, with every identity/output/ownership/quiet/join/RAM/archive gate
passing. Independent SHA verification passes; no users remain.

| Final retirement form / API | Elapsed change | Wider CPU change | Reference / candidate control |
| --- | ---: | ---: | ---: |
| Captured text / Run | −1.51% to +3.47% | −2.13% to +9.09% | 0.66% / 4.19% |
| Ordinary text / Run | −6.18% to −1.03% | −5.69% to −0.83% | 3.70% / 1.72% |
| Keyword-default search / Run | +10.87% to +17.24% | +6.62% to +9.07% | 1.92% / 3.61% |
| CSV / Run | +0.56% to +1.53% | 0.00% | 0.77% / 0.20% |
| JSON-to-Markdown / RunAsync | +3.36% to +4.11% | +2.08% to +8.51% | 0.09% / 0.64% |

These use the same boundary and range definition as above. Final async text CPU
is not qualified by this five-case group. The allocation and ordinary-text gains
do not justify the material search/report regressions and shared call/return
invariants. Removing unconditional metadata writes did not close that debt; the
precise generated-code cause remains unresolved. This is a decision on this
implementation, not a fixed percentage veto or permanent closure of the idea.

[Decision](decision.json) records the withdrawal. After restoration,
[nonincremental Release builds and focused regressions](restored-checks.json)
pass: 38 internal and 78 public checks per actual target, including the newly
retained external coverage. Existing full `8f8628a4` delivery remains applicable;
no unchanged platform/package run is repeated. Per-line carriers and preparation
reservations remain in the restored runtime.

The [next source gate](next-source-gate.json) assesses private word materialization/
probing while preserving ordinary call/return transport and current ownership.
Current source has 3,072 lines/6,144 words and existing UTF-8 span hash/probe support.
Final bindings, inserted-key identity, callbacks, fallback and errors require a
complete publication/funding proof before another prototype. No new destination
channel, cache/pool or lifetime ledger is selected. The performance objective
continues; no new CPython ratio follows from this investigation.

[Initial source identity](initial/diagnostic-identity.json),
[initial Run](initial/warm-sync/summary.json),
[initial RunAsync](initial/warm-async/summary.json),
[refined identity](refined/diagnostic-identity.json),
[refined Run](refined/warm-sync/summary.json),
[refined RunAsync](refined/warm-async/summary.json),
[quiet summary](refined/quiet/summary.json),
[raw quiet observations/core](refined/quiet/observations.tgz),
[receipt](refined/quiet/archive-receipt.json),
[final form](retirement/diagnostic-identity.json),
[final Run](retirement/warm-sync/summary.json),
[final RunAsync](retirement/warm-async/summary.json),
[final quiet summary](retirement/quiet/summary.json),
[final raw observations/core](retirement/quiet/observations.tgz),
[final receipt](retirement/quiet/archive-receipt.json),
[goldens](python-goldens.json) and [canonical images](reference-images.json)
retain the evidence. Each form includes exact before/candidate source archives,
actual-target images and compressed check records. Supplemental patches normalize
line endings; source archives preserve exact bytes. The current external baseline
remains [BENCHMARKS](../../../docs/BENCHMARKS.md); no new CPython ratio is inferred.
