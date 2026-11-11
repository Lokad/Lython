# Native dictionary counting loops — 2026-11-11

Retained in `fe6648b2`, above retained runtime `698f7e77` at evidence HEAD
`1ce7fbbc`. The compiler prepares an entire histogram loop, optionally including
an integer total, using existing native iteration, dictionary arithmetic and local
binding. It removes repeated instruction dispatch and call-slot preparation.
No new value representation, ownership system or execution backend is introduced.
The ordinary body remains necessary for protocols, growth and genuine suspension;
refused admission throws rather than selecting that continuation.

Both source snapshots and actual .NET 11/10 binaries are frozen. The measured
precommit candidate has `1ce` assembly metadata; all 568 production source hashes
match committed `fe6648b2`. Subsequent exact-revision delivery
[43024046530](https://github.com/Lokad/Lython/actions/runs/43024046530) passes
Windows/Ubuntu, actual .NET 11/10, original stress and installed Release consumers;
see the [recorded result](delivery-ci-status.json). The original decision retains
its then-pending status. Later runtime changes need separate qualification.

## Quiet source-pair confirmation

| Complete warm script | API | Candidate elapsed change | Reference / candidate control spread | Max IQR |
| --- | --- | ---: | ---: | ---: |
| Captured text helper + counter | Run | −11.40..−9.44% | 0.59% / 1.61% | 1.78% |
| Ordinary text helper | Run | −20.91..−12.88% | 7.34% / 2.02% | 1.33% |
| Captured text helper + counter | RunAsync | −13.26..−7.27% | 2.80% / 3.99% | 2.12% |
| Ordinary text helper | RunAsync | −19.69..−11.81% | 5.06% / 4.25% | 1.11% |
| Captured CSV helper/control | RunAsync | −1.76..+2.74% | 1.06% / 3.47% | 1.53% |

Negative means faster. Ranges include all four pairings of independent process
medians, not confidence intervals. Captured synchronous text has the tightest
controls. Other text rows support the direction with less precise magnitude.
Wider process-CPU measurements agree directionally for text, with coarse tick
resolution. CSV neutrality is unresolved; no CSV gain is claimed.

The existing quiet Azure VM, stock .NET 11 RC3, normal GC/tiering and fixed common
`fec79` comparison worker ran five jobs in 140.881 s. Each image warms for five
seconds; seven rotating rounds target 0.15 s per batch. Warm includes mediated
RAM-file I/O, full output checks and prior-result cleanup. Compilation, startup,
fixture reset, protocol and final caches are excluded. All loaded core/auxiliary
hashes, quiet gates, output hashes and joined fixture cleanup pass. All raw data
was archived and verified under the owned lease, then independently verified
after retrieval. B576 is terminal and cached; never query it again.

## Allocation, compilation and adverse data

The separate Windows desktop comparison runs six complete scripts through both
APIs and measures compilation separately. Runtime allocation and admitted peaks
are effectively unchanged. Compilation allocation grows only about 0.02–0.03%
for text preparation. Desktop elapsed data is mixed/adverse with unstable unchanged
controls, including ordinary text sync +2.99..+23.34%; it does not establish CPU
improvement or neutrality. Quiet clocks are a separate confirmation, not a
replacement for those observations. The local `9a4272de` worker differs from the
fixed quiet worker; never combine their clocks.

## Evidence and limitations

- [Decision and remaining regression debt](decision.json),
  [quiet summary](quiet/summary.json), [complete quiet archive](quiet/observations.tgz).
- [Local identity](diagnostic-identity.json), [baseline identity](before.json),
  separate [sync](warm-sync/summary.json), [async](warm-async/summary.json) and
  [compilation](compile/summary.json) summaries, with raw observations beside them.
- [Independent CPython 3.13 semantic goldens](cpython-goldens-complete.json):
  protocol calls/equality, list mutation, empty/final bindings, aliases and integer
  promotion. Pending callbacks, active cancellation, retained results and refused
  growth also pass focused tests on both targets.
- [Exact prepared instruction windows](instruction-prepared-total.txt),
  [original source/fixture/build corrections](corrections.json), source archives,
  compressed cores and preparation archives preserve the complete conditions.

The earlier `15f` versus `4aa` ordinary-text async regression remains historical
debt; this `698` source pair does not directly remeasure it. RSS, real pending-I/O
latency and embedding tail latency are unqualified. A separate source-labelled
CPython checkpoint measures external performance; these source-pair percentages
are not Lython/CPython ratios. Publication remains postponed.
