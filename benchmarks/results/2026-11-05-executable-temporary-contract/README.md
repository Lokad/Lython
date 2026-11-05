# Executable temporary retirement

Source2183bf18 plus the archived D50 patch replaces the internal
IExecutableTemporaryValue interface with one empty abstract ExecutableTemporaryValue
base. The same four sealed classes retain their disposal bodies: iteration cursor,
inlined-comprehension scope, call-argument accumulator and governed byte builder.
No instances, fields, owners, arrays, disposal modes or public options are added.
The displaced interface is deleted. Ordinary guest IDisposable values are not
retired as interpreter temporaries. Reverse order, failed-disposal live extent,
slot clearing and awaited pending-cursor cleanup retain their existing semantics.

## Source attribution

D49 reuses the immutable [D43 capture](../2026-11-05-current-script-cpu/README.md),
not a new profile. Of783text/1022ordinaryCSV guest samples,53/42 have interface-test
leaves. Their first Lython callers include RetireTo27/32 and target instruction24/10.
Those implementations were unchanged since D43. Inlining,5–8% unknowns and one
truncated map line per case limit attribution: these are not removable CPU shares.
See caller-assessment.json and offline-callers.py (supply the extracted perf folder).

## Whole-script observations

Same immutable current core2f315485 and reference3fa1389d in both blocks;
workerb94, CPython3.13.16, Azure F4as_v7, ordinary GC/tiering. Reference source is
2183bf18; binary3fa was frozen before its commit, so commit metadata may differ.
Identity files distinguish source hashes from observed DLL hashes.

| Script/lane | Initial current/reference change, two current processes | Confirmation, two current processes |
| --- | ---: | ---: |
| Text helpers, warm |−0.34% /−4.01%|−4.00% /−5.53%|
| Ordinary CSV functions, warm |−0.93% /−3.90%|−2.89% /−0.96%|
| JSON helpers, warm |−3.32% /−2.27%|—|
| Report rendering, warm |−3.72% /−2.80%|—|
| Text helpers, completed-host async |−8.08% /−4.24%|+0.99% /+0.58%|

Negative means lower elapsed. Initial140.27s/five jobs: independent identical-current
roles differ3–4% in several cases; report reference IQR5.49%. Preserve these data.
That justified one unchanged-binary confirmation with longer batches:
114.57s/three jobs, eleven rotated~300ms samples instead of seven~150ms,
five-second engine warmup/job. Both original180s owned bounds unchanged.
Confirmation text current spread1.59%,CSV1.99%,async.41%; async control IQR1.68%.

**Retain the complete nominal contract and focused semantic control.** The warm
text improvement is supported in the confirmation, while CSV magnitude varies.
Async changes conflict between blocks; the adverse~1% result remains unresolved
regression debt, not a proven causal regression or a closed debt. No allocation,
object-layout, attributed CPU, broad/portable speedup or actual delayed-I/O latency
claim. No further retiming of this isolated change is scheduled. Future complete
redesign checkpoints must preserve this uncertainty and the original conditions.

Boundaries include invocation, actual mediated /dev/shm filesystem I/O, full
output/file validation and prior-result cleanup. Compilation/startup, fixture
staging/reset, transport and final cache/owner/process/fixture teardown are outside
the timing. Completed-host async is separate from actual suspension correctness.
All quiet, source/fixture/golden/library identity, Closed/final-zero funding,
joined-worker and removed-fixture gates pass. Both immutable observations archives
and receipts are retained. Confirmation's copied receipt initially said5jobs;
the authoritative raw data has3. Both receipt versions remain, archive unchanged.

## Validation and delivery

Focused Release223internal/514public per actual.NET11/10 pass, including
comprehensions, generators, argument expansion, formatting, cancellation, terminal
refusal, retained aliases and cleanup. The new boundary control proves retiring
ordinary IDisposable values does not dispose them. Full retained-source platform,
stress and installed-package qualification is tracked at the coherent delivery
checkpoint. Retained D50 plus D51 diagnostics: final Release build20.72s/zero
warnings/errors, full4125internal/16004public per actual.NET11/10 pass. Publication
remains postponed. The broader performance objective is open.
