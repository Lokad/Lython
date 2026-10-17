# Prepared asynchronous call allocation diagnostic

One local Windows capture on 2026-10-17 UTC compares Lython `7ac0d4c0` with
the [frozen two-file patch](candidate.patch). Each sample executes 128 warm
native `sorted` calls plus one `range(128)` construction. Both APIs include
interpreter/call/result handling. Source, input, context and compilation already
exist; startup and host I/O are excluded. The call form occurs in the complete
text/CSV/JSON/file jobs; this is not a timing of those jobs.

| Call form | Synchronous baseline bytes | Candidate bytes | RunAsync baseline bytes | Candidate bytes |
| --- | ---: | ---: | ---: | ---: |
| `sorted(values)` | 133,760 | 133,760 | 154,328 | 142,976 |
| `sorted(values, reverse=True)` | 135,808 | 135,808 | 156,376 | 145,024 |

Each cell is the median of eight current-thread managed-allocation samples after
four warmups. All baseline and synchronous candidate samples equal their medians.
One async keyword candidate sample is 32,664 bytes high; every other candidate
sample equals its median. The 11,352-byte reduction is consistent with removing
88 bytes of closure/delegate transport for each of the 129 generic calls.
No elapsed-time, CPython multiplier, RSS or embedding-latency benefit is proved.

The executable async caller already has its argument array. It now passes that
array through the existing prepared/deferred distinction rather than creating
a capturing factory just to return it. Both forms share one async frame/check/
translation/cleanup body. Genuinely deferred lowered argument evaluation remains
deferred. The caller awaits invocation before scrubbing/returning argument
storage. No cache, dispatcher mode, budget, owner or reclamation policy is added.

Managed [IL evidence](dispatch-il.json) for the captured candidate shows the
argument field retains its generic value type, two constrained argument calls
and no `box TArguments` instruction in the shared async state machine. The
[one-off inspector](inspect-dispatch.ps1) uses managed reflection only. This
specific check is not a claim that all other invocation objects are unboxed.

Existing suspended-call tests were strengthened before capture: they inspect the
actual positional/keyword buffer before and after suspension, including callback
failure, alongside cancellation/frame/result-admission controls. All 52 selected
controls and diagnostic cases pass before and after. They supplement the delivery
regressions; the allocation comparison alone does not prove lifetime correctness.

[Evidence](evidence.json) records all samples, source/binary identities, process
receipts, machine and SDK. Raw [baseline](baseline-allocation.trx) and
[candidate](candidate-allocation.trx) preserve output. The exact test-runtime
patch was not captured. There is no forced GC or long benchmark lane.

The [temporary probe](PreparedAsyncAllocationProbe.cs.txt) was removed before
final qualification. Reproduce with revision `7ac0d4c0`, copy the probe into the
white-box project, build Release and select `PreparedAsyncAllocationProbe`, then
apply the frozen patch and repeat once. Remove the temporary source afterward.
Keep the existing 28-second owned-process cap plus one second for cleanup.
This is archived evidence, not a new benchmark framework or ordinary threshold test.
