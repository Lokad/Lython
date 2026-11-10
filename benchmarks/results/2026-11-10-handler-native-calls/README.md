# Closed helper calls inside exception handlers

The retained candidate removes the caller's active-handler exclusion from the
existing closed-native function entry. A proven closed helper neither binds nor
unwinds its caller's handler aliases. Global resolution and current exception
state remain shared; class, annotation, class-cell and comprehension restrictions
remain. General context deferral is unchanged. No new engine, cache or policy is
introduced. The useful post-frame shortcut assessed in R177A is preserved.

This removes binding/frame construction from a previously overconservative path,
not from every function call. Ordinary eligible calls already use direct entry.
Six independently captured CPython 3.13.2 scripts cover aliases, parameter/builtin
shadowing, nested exception context, class cells, annotations and retained values.
Final 79 focused public cases pass on each actual .NET 11/.NET 10 target, including
genuinely suspended custom protocols inside active handlers, cancellation and
joined owner cleanup. Full delivery qualification is recorded below.

| Complete text script enclosed in an active handler | CLR allocation | Quiet elapsed | Wider process CPU |
| --- | --- | --- | --- |
| Synchronous API | −47.31% | −18.19–20.70% | −18.06–20.55% |
| Asynchronous API | −47.29% | −19.35–22.05% | −18.18–22.39% |

Allocation uses the existing isolated-image desktop driver, with R178 before/after
images differing only in this eligibility correction. Desktop elapsed remains
diagnostic: synchronous −4.20–6.20%, asynchronous +0.06–1.95%; compilation allocation
is unchanged. The original six-case catalog rejection occurred before timing and
is preserved alongside the corrected five-case source and all raw lanes.

Quiet B522 uses qualified `cac4acf8`-equivalent production against `fcf018d2` plus
R178/R179. The affected text script performs no JSON serialization, so the two
terminal JSON changes are not exercised there. Its source, fixture and goldens are
the same as B521. This is a Lython revision comparison, not a CPython timing.

All five jobs completed in 142.139 seconds with four processes/two per source,
five-second role warmups and seven rotated 150 ms rounds. Includes mediated RAM
I/O, validation and prior-result cleanup; excludes startup, fixture reset, protocol
work and final caches. Process CPU has a wider boundary. Machine/toolchain and
exact binary identities are in [quiet summary](quiet/summary.json).

Adverse controls remain unresolved: CSV elapsed −1.93–0.21% with CPU overlapping;
report elapsed +3.35–9.84%/CPU +3.08–16.62%, candidate process spread 5.28%; Unicode
elapsed +2.25–7.25%/CPU +2.61–10.46%. A bounded ordinary-workload confirmation is
justified; these results do not establish a universal improvement or equivalence.

All output/balance/isolation/quiet-host/join/unmount/archive gates pass. The archive
was verified under owned cleanup and independently SHA-verified locally:
`4ac09a7a2b79e0787e8bfcae1d34db39c80d91be853b648161993fd5cc1b9e7e`.
No users or RAM fixtures remain; terminal status is cached. Three exact duplicate
remote compressed copies were removed only after proving their bytes survive as
members of tracked local immutable archives; raw trees/frozen binaries remain.

[Source/check archive](source-checks.tar.xz), [Python goldens](cpython-goldens.json),
[desktop summary](desktop-summary.json), [owned VM archive](quiet/observations.tgz)
and [status](status.json) preserve identities, conditions and limitations. Publication
remains postponed. The combined full platform, Stress and installed-consumer checkpoint is separate.

The [bounded ordinary-control confirmation](../2026-11-10-handler-native-call-controls/README.md)
completed in59.644s: report elapsed+0.14–2.01%, CPU overlapping; Unicode
elapsed−1.28–2.72%, CPU overlapping. It does not reproduce the original adverse
magnitudes or prove equivalence. Both original outcomes survive.

Delivery source `ee448715` is fully qualified by
[42727417102](https://github.com/Lokad/Lython/actions/runs/42727417102): Windows and
Ubuntu, actual .NET 11/.NET 10, original Stress and installed Release package
consumers all pass. The [cached terminal receipt](../2026-11-10-handler-native-calls/delivery-ci.json)
changes no measured source or observations. It does not qualify later prototypes.
