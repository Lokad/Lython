# Shared binding destination and closed named/default calls

R184 / B532, 2026-11-10. Retained coherent source above d3b929a2; public default
regressions already committed in 75c2bfc5. Reference production is ee448715,
qualified by cached CI 42727417102. Candidate core f8995f65; reference b49e50a6.
Only the core differs; exact hashes, full source delta and test logs are preserved.

One shared Span/ref-presence fill replaces the array-only binder. Proven small
closed named/default helpers bind into managed inline storage and use the existing
native evaluator, avoiding owned workspace/activation. Exact-positional borrows
are unchanged. Wide/dynamic/protocol defaults retain owned binding and real async
suspension. There is no new binder, arena, owner, pool or public option.

## Controlled complete-script comparison

Quiet Azure F4as_v7 / Ubuntu 24.04.4 / .NET 11 RC3, ordinary GC/tiering.
Three synchronous warm jobs, four independent processes (two per source), five-
second warmup per role and seven rotated 400 ms rounds. No CPython timing lane.

| Complete script | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| scripts.file-search.large | -21.51 to -15.71% | -18.83 to -15.37% |
| scripts.text-summary-helpers.large | -1.78 to +2.79% | -4.27 to +0.56% |
| scripts.csv-validated-records.large | -2.16 to +3.49% | -1.19 to +6.19% |

Ranges span all four process-median pairings, not confidence intervals. Search
reference spread is 6.75%, candidate 0.60%; the gain survives every pairing.
Text/CSV controls are inconclusive, with possible small regression debt. CPU uses
100 Hz process ticks and includes wider reset/protocol work. No universal gain,
equivalence or isolated internal CPU attribution is claimed.

The search uses the maintained full script/input/output with a useful keyword-only
prefix default replacing the literal in its helper. Its source SHA differs from
published CPython rows. Independent CPython 3.13.2 validates identical stdout/files;
it is not a new CPython performance comparison. Text/CSV controls are unchanged.

Warm execution includes mediated RAM I/O, complete output validation and prior-
result cleanup. Compilation, startup, fixture reset/protocol and final caches are
excluded. Desktop compilation is measured separately, including construction/
disposal; allocation is flat. Desktop warm search allocation changes about −0.05%,
while its elapsed +5.46–8.31% adversity is preserved and not causally explained.
Quiet results support the source change; desktop timings remain diagnostic.

## Semantics and ownership

Focused Release checks on the isolated final source pass 715 public / 82 internal
cases per actual .NET 11/10. The actual runtime-defined keyword-default prefix
helper runs with further admission denied, without extra reservation and with
existing inputs funded. Default alias/replacement, argument diagnostics, wide
calls, protocol suspension, cancellation, terminal refusal, retained results,
stack admission, cleanup and budget reuse remain covered. Full delivery checks
follow the retained commit; this evidence does not predeclare their result.

All three jobs finish in 112.098581092 seconds. Identity/output/balance/quiet/
worker-join/unmount/archive-under-owner/local-SHA gates pass. No VM users or RAM
fixtures remain. Exact duplicate retired prototype cores survive in tracked
canonical archives; every hardlink alias was proved under an idle lease before
retirement. The first staging attempt refused an old stage pathname before launch;
its source/failure and the unique-path correction survive. No worker run restarted.

**Decision:** retain the actual binding/workspace removal and measured targeted
CPU benefit. Allocation reduction is negligible. Accept unresolved small control
and desktop compilation timing debt; no compiler/storage work increased. Wider
calls remain supported on the existing route. Continue the broader call/value/
record roadmap; this milestone does not complete the performance objective.

[Raw observations and frozen cores](observations.tgz), [controlled summary](summary.json),
[desktop measurements, failed conditions and source checks](development.tar.xz),
[source delta](source.patch), [source identities](final-source.json).
