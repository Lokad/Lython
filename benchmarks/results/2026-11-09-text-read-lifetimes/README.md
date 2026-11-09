# Text-read operation lifetimes

R125, 2026-11-09 Paris / 2026-11-08 UTC. Qualified runtime6f76/coreC617 versus
working source after a9ef3e1c, final core004e8446. This is a correctness fix;
there is no claimed CPU improvement.

Both production-reference regressions failed: cleanup requested from an output
admission callback refunded342bytes, including the still-borrowed72-byte raw array.
The native read now owns raw/decoded storage through copying, decoding, appending,
actual host suspension and requested cleanup. Async disposal is awaited without
blocking entry. Buffered copying may finish; another refill cannot start after
retirement. Reentrant reads fail explicitly. Independent returned strings keep
their funding. Failed cleanup cannot replace the operation's original failure.
No new public option, memory allowance, ledger, tracing or background work.

[Focused checks](focused-validation.json):228 unique internal/140public Release
cases peractual.NET11/.NET10.16new cases include copy/append/decode retirement,
in-flight cancellation, first structured refusal, pending transport, slow disposal,
cleanup failure precedence and return to baseline. An initial exact-type fixture
mistake expected the base runtime error instead of its HostOperationException
subclass; corrected without changing runtime, original failed log retained.

Two short desktop diagnostics use five unchanged complete scripts, both execution
APIs, the same worker/optional0.4 and qualified C617 reference. Allocation is
effectively neutral (all differences within0.07%); elapsed is unstable and adverse
results survive. Initial async text+5.73–7.98%; refined sync text+12.83–13.47% and
report+24.60–40.91%, while refined async text−3.90–3.37% and report−9.21–8.82%.
An added redundant scanner poll was removed after entry acquired the check;
bounded refill/completed-line cancellation remains. These source refinements and
desktop reversals do not establish CPU effects or explain every adverse sample.

B485 controlled confirmation: quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/tiering,
four independent processes/job,5s warmup/role,7rotated150msrounds, three jobs86.526s.
Existing complete text script, both APIs, and complete report control. Compilation
and startup excluded; mediated RAM-file I/O, full goldens and prior-result cleanup
included. Fixture reset/protocol/final cached teardown excluded. Same core/auxiliary
identities, Ready/Closed, goldens/budget balances, quiet before/after, joined users,
owned-RAM removal, archive-under-lease and local archive/hash verification all pass.

| Script / API | Candidate elapsed vs both reference processes | CPU change | Reference / candidate process spread |
| --- | ---: | ---: | ---: |
| Text/helpers, sync | −1.19..+2.62% | −1.24..+3.75% |2.92% /0.81% |
| Text/helpers, async | −2.78..−0.22% | −3.34..+2.06% |1.86% /0.72% |
| Report control, sync | −1.01..+0.89% | −4.26..+0.39% |1.71% /0.21% |

The adverse async-text desktop result does not reproduce here. Sync text and coarse
process-CPU ticks leave uncertainty; these short results establish neither a
speedup nor absence of every regression. Retain for demonstrated ownership
correctness; no further unchanged retiming selected. R125 full delivery now passes Windows/Ubuntu actual.NET11/.NET10 Release, original
stress on both targets and installed-core consumers: [cached CI42536319061](delivery-ci.json).
Observed Nov9 00:08:01UTC; qualifies3109/core004e, not later R126. Never requery.

Two launch preflights refused insufficient persistent free space before extraction
or measurement. Reuse of a SHA-verified existing catalog and removal of redundant
remote source-inventory metadata allowed launch without reducing the240MiBOS/
12MiBevidence floors. Full original inventory remains local, hash-linked from the
remote proof. No historical files deleted/replaced and no space-recovery campaign.
Original payloads/preflight logs survive. All B485 users joined, owned RAM removed;
terminal result cached, never requery. Publication postponed; broader roadmap open.

[Source identity](identity.json), [initial diagnostic](initial-assessment.json),
[refined diagnostic](refined-assessment.json), [quiet comparison](assessment.json),
[archive receipt](archive-receipt.json), [raw quiet evidence](observations.tgz),
[local source/conditions/diagnostics](local-source-and-diagnostics.tar.xz).
