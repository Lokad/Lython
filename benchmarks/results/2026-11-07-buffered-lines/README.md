# Shared buffered-line execution — R66/B420

2026-11-07. Retain the shared line scanner and synchronous buffered prefix.
Current core `15c04e2a` / `5dd131bf` plus frozen R66 patch; local baseline
`77993f5c` isolates R66 on R65. The VM baseline is fully qualified `70267889` /
`74c88248`, so its source pair includes R65 and R66. Normal 0.4 optional adapter
and comparison worker remain unchanged. Exact source/Ready/auxiliary identities
are archived; no speedup is inferred merely from eliminating async source.

Readline and native destination reads use one window scanner/emission routine.
Buffered reads return directly; only a pending refill creates the iterative
continuation. It owns the private builder through actual read completion and
unwind. Caller-owned CSV destinations are never released here. Async file iteration
and simple forwarding wrappers also complete directly. Duplicated sync/async line
algorithms and per-line forwarding state machines disappear: 14 net production
lines are deleted. Existing decoder, CR holdback, newline/scalar bounds, allocation
preflights, alias funding, cancellation, first refusal and awaited close remain.
No new public option, owner, backend, result framework or memory mechanism.
Cooperative cancellation can be observed earlier on buffered reads; exact private
checkpoint locations are not a contract. Public APIs and supported syntax remain.

**637 internal / 3,584 public focused Release checks per actual .NET 11 and 10
PASS.** Matching probes contain corresponding core bytes. Twelve new public cases
use a two-byte decoded window, then a genuinely pending refill and close, covering
readline, iteration, CSV and JSON-lines under completion/cancellation/refusal.
Storage remains funded until real users finish; suppression/finally return and a
secondary close error cannot mask the first refused estimate. Those same twelve
cases pass on the previous R65 core, confirming independent contract coverage.
The initial test fixture compile failure (underscore parameter/discard collision)
and its correction are preserved. No timeout or containment assertion was weakened.

## Controlled complete-script comparison

Quiet F4as_v7/Ubuntu 24.04.4/.NET 11 RC3; four independent baseline/current/
baseline-control/current-control processes, normal GC/tiering, five-second warmup
per role/job, eleven rotated 200 ms rounds. Six jobs complete in 197.560 seconds.
Full stdout/file goldens, source/core/Ready/auxiliary, quiet endpoints, Closed/exit
zero/join/funding/emptyfixtures pass. Persistent evidence is separate from the owned
128 MiB fixture tmpfs, archived/verified under lease and locally; mount removed.
240-second owner + 30-second stop + 30-second margin; observe after 300 seconds.
Terminal/cached; never query B420 again.

Warm clocks include public Run or awaited RunAsync, mediated RAM I/O, full output
validation and prior-result cleanup. Compilation, startup, fixture staging/reset,
transport and final cached teardown are excluded. CPython was not rerun here;
the primary comparison table still describes its recorded `55e510ff` revision.

Positive percentages mean current slower. Ranges compare both current processes
with both baseline processes; CPU is whole-worker CPU, quantized to 10 ms and
including work outside the internal elapsed boundary.

| API / complete script | Elapsed changes | Worker CPU changes | Old / current process spread |
| --- | ---: | ---: | ---: |
|warm-async / csv-summary-functions.large|-4.31 to -0.31%|-4.26 to +0.00%|+0.56% / -3.47%|
|warm-async / text-summary-helpers.large|-8.26 to -2.32%|-7.22 to -2.11%|+3.20% / -3.07%|
|warm-async / json-summary-helpers.large|-3.96 to -1.46%|+0.00 to +0.40%|-0.74% / +1.83%|
|warm / text-summary-helpers.large|-4.43 to +0.55%|-3.77 to +0.95%|+4.23% / +0.94%|
|warm / csv-summary-functions.large|-2.72 to +0.99%|-3.70 to +0.00%|-1.08% / -2.63%|
|warm / report-render.large|-4.94 to +2.39%|-5.00 to +6.94%|+4.39% / +3.19%|

Async text and JSON elapsed improve across both candidate processes. Their CPU
observations differ: text improves, JSON is approximately unchanged. Baseline and
current process spreads reach several percent, and synchronous/report observations
include adverse elapsed/CPU points. Within-role precision does not remove between-
process variation. Hashing/JIT/scheduling may contribute; none is established as
the cause. **No general CPU or synchronous-execution speedup is established.**
Preserve the +0.99% synchronous CSV, +2.39% report elapsed and +6.94% report CPU
points and treat remaining magnitude/impact as uncertainty. Retain the coherent
deletion and bounded async elapsed evidence; do not declare the whole-script
objective complete or use a fixed percentage veto. A future bounded confirmation
can control shared hashing/GC or increase process replication if it changes a
decision; no unchanged retiming campaign was run here.

The existing five-script desktop diagnostic independently passes all goldens,
isolation, shared funding and fixture cleanup through both APIs. Allocation is
essentially unchanged. Original async CSV +7.45/+13.94% and large identical-role
drift remain; they are not evidence of a controlled CSV regression or CPU benefit.
Do not combine absolute desktop and VM clocks: their fixture/reset boundaries differ.

Initial staging stopped before measurement at the 256 MiB reserve. Only the two
redundant extracted B418 baseline copies were removed after exact member bytes and
the local/remote immutable archive hashes were verified; archived evidence remains.
The chosen B418 candidate baseline was unchanged. Setup then passed with
271,523,840 free bytes. Original failure, guarded reclaim and launch records remain.

Full combined R65/R66 Windows/Ubuntu, both targets, original Stress and installed
core-consumer qualification is pending at this delivery checkpoint. Publication
remains postponed. R62's cached green receipt does not qualify newer source.

[Controlled assessment](assessment.json), [desktop assessment](local-assessment.json),
[focused checks](final-focused.json), [exact VM source/core inventory](vm-identity.json),
[pre-code plan](pre-code-plan.md), [VM receipt](archive-receipt.json),
[VM raw/source/cores](observations.tgz),
[local source/cores/tests/failures/raw observations](local-observations.tgz).

Combined R65/R66 source824fc6bb is **FULLY QUALIFIED**, CI42418518578:
Windows/Ubuntu Release actual.NET11/10, original Stress at both targets and
installed core-package consumers all pass. [Cached delivery receipt](../2026-11-07-buffered-lines/delivery-ci.json).
Never query the terminal run again. Launch-time pending statements in immutable
identities/plans remain historical. This does not qualify later runtime source.
