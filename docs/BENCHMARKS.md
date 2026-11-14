# Performance against CPython

| Representative workload, warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice / pricing helpers|4,096 records /127,031 B|7.600–7.873 ms|6.020–6.047 ms|1.26–1.31×|Variable; spreads3.47/0.45%; maxIQR1.82%|
| Text / captured helper and counter|3,072 lines /35,840 B|1.327–1.421 ms|0.891–0.897 ms|1.48–1.59×|Variable; spreads6.57/0.62%; maxIQR1.70%|
| JSON-lines / aggregation helpers|4,096 records /110,592 B|3.407–3.416 ms|4.103–4.129 ms|0.83–0.83×|Diagnostic; spreads0.29/0.62%; maxIQR1.86%|
| File search / keyword-default helper|2,049 lines /42,010 B; stop after1,025|269.828–280.750 µs|159.672–162.647 µs|1.66–1.76×|Variable; spreads4.05/1.83%; maxIQR1.67%|
| JSON-to-Markdown report|3,072 records /189,441 B|2.782–2.800 ms|2.321–2.337 ms|1.19–1.21×|Diagnostic; spreads0.63/0.67%; maxIQR4.81%|
| CSV fulfillment / pricing and validation|4,096 records /126,821 B|8.840–8.878 ms|6.764–6.813 ms|1.30–1.31×|Diagnostic; spreads0.43/0.73%; maxIQR1.77%|
| CSV validation / for-loop checksum|4,096 records /49,163 B|5.367–5.390 ms|4.182–4.196 ms|1.28–1.29×|Diagnostic; spreads0.43/0.35%; maxIQR1.44%|
| Configuration JSON edit |96 B|75.3–76.4 µs|59.6–59.7 µs|1.26–1.28×|Diagnostic; spreads1.56/0.15%; maxIQR1.55%|

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** These are
complete embedding measurements, not isolated interpreter CPU. Spreads are the
Lython/CPython process-median differences; moving controls and IQRs remain visible.

First seven rows measured2026-11-14UTC: **009d236a / frozen8ae1ae5f**,
against stock**CPython3.13.16**. Full Windows/Ubuntu .NET11/10, original stress
and installed-package qualification
[passes43563853239](../benchmarks/results/2026-11-14-indexed-sequence-cursors/delivery/README.md).
[Exact sources, raw/wider CPU and controls](../benchmarks/results/2026-11-14-iteration-cpython-checkpoint/README.md).
Invoice/search warm process spreads3.47/4.05% and captured text6.57% remain
inconclusive stability despite smaller within-process IQRs. The prior measured
revision and adverse conditions stay in the linked evidence; cross-collection
differences do not attribute individual changes.
The96 B configuration row uses **cdd92944 / frozenf4c4f027**, measured2026-11-14;
its full platform/stress/package qualification
[passes](../benchmarks/results/2026-11-14-incremental-xml/delivery/README.md).
[Configuration phases and exact evidence](../benchmarks/results/2026-11-14-config-phases/README.md).
The quiet Azure F4as_v7 VM runs Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs,
16 GiB/no swap and .NET 11 RC3, ordinary workstation GC/tiering and CPython GIL/GC.

Warm clocks include mediated RAM-file I/O, full stdout/file validation and
prior-result cleanup. They exclude compilation, startup, fixture staging/reset,
protocol and final cache teardown. Two independent processes per engine, five
rotating rounds. Ranges span process medians and ratios all four pairings;
they are not confidence intervals. Warmup and batch sizing use full-request
wall time; guest-execution clocks remain unchanged. This refresh does not
attribute differences from older collections to individual changes.
[Previous checkpoint and all adverse observations](../benchmarks/results/2026-11-13-current-cpython-checkpoint/README.md).

## Compilation, compile-to-result and startup

The invoice and96 B configuration sources separate frontend cost from execution:

| Phase | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| Invoice compilation alone|101.315–102.321 µs|93.146–93.415 µs|1.08–1.10×|Diagnostic; spreads0.99/0.29%; maxIQR1.11%|
| Invoice compile and execute once|7.715–7.778 ms|6.267–6.380 ms|1.21–1.24×|Diagnostic; spreads0.82/1.80%; maxIQR1.58%|
| Configuration compilation alone |36.6–37.3 µs|34.3–34.4 µs|1.06–1.09×|Diagnostic; spreads1.94/0.17%; maxIQR1.97%|
| Configuration compile and execute once |136.5–137.3 µs|121.4–124.2 µs|1.10–1.13×|Diagnostic; spreads0.61/2.27%; maxIQR3.65%|
| Process startup |—|—|—|No current comparison|

Compile-to-result includes compilation and compiled-owner disposal, plus warm
execution costs above. Wider process CPU includes fixture reset/protocol.
Startup, shared-budget contention, genuinely pending I/O, RSS/heap peaks and
embedding tail latency remain unmeasured. The
[previous fully qualified checkpoint](../benchmarks/results/2026-11-13-fixed-definitions-cpython-checkpoint/README.md)
retains its own exact revision, boundaries and adverse/variable observations.

## Additional qualified ZIP comparison

Source 976f9439 / frozen 97bbeff3 adds opened positional input. Its complete warm
archive-config script measures 58.8–59.9 µs versus CPython's 56.6–57.1 µs for a
1,048,827 B archive (1.03–1.06×, variable). Prior Lython took 263.8–281.9 µs;
the new path is roughly 77–79% faster, with a variable reference. Sequential hosts
retain buffering. A 16,635 B archive is about 2.2–2.5 µs slower than prior Lython,
while remaining around CPython's time. Accept this recorded small-case debt for
the bounded-input architecture; no universal speedup or tail-latency gain is claimed.
[All controls and original failed conditions](../benchmarks/results/2026-11-13-positional-zip/comparison/README.md).
This source passes Windows/Ubuntu .NET 11/10, stress and installed-package
checks in [43465190359](../benchmarks/results/2026-11-13-positional-zip/delivery/README.md).

## Useful historical cases not refreshed here

These compile-to-result observations describe their named revisions, **not HEAD**:

| Workload / input | Lython | CPython | Ratio | Historical source and status |
| --- | ---: | ---: | ---: | --- |
| Config edit /181 B JSON |138.7–139.3 µs|124.8–125.1 µs|1.11–1.12×|41bd869b; diagnostic|
| CSV validation /5,120 records |4.170–4.249 ms|4.464–4.520 ms|0.92–0.95×|2d91eb1a; diagnostic; other lanes partial|
| Ordinary text summary /3,072 lines |1.295–1.323 ms|1.290–1.308 ms|0.99–1.03×|983bab96; variable|
| Copy/checksum/manifest /4,107 B |0.384 ms|0.260 ms|1.47–1.48×|459813a3; diagnostic|
| Patch generation /two1,024-line files |0.556–0.558 ms|0.647–0.651 ms|0.85–0.86×|4aa2d46d; diagnostic|

Earlier warm text had12.40%Lython process spread; the CSV campaign's other lanes
lacked their final gate. Those inconclusive observations, replaced rows, exact
dates and source links remain in the [previous guide](../benchmarks/results/2026-11-13-fixed-definitions-cpython-checkpoint/prior-BENCHMARKS.md).
Cross-collection differences cannot attribute a gain to a particular optimization.

## Improvements within Lython

CPython remains the external baseline. Separate source-pair experiments establish:

- Native helper returns: historical whole-run allocation falls19.92%for CSV helpers
  and19.31%for JSON helpers;
  original elapsed observations are diagnostic.
- Direct UTF8 formatting/storage: historical allocation falls8.67%; the original
  adverse clock remains preserved. Later shared formatter comparisons support
  report CPU/elapsed direction on their specific sources.
- Native integer chains remove16,384boxes/524,288 B per invoice invocation:
  allocation falls8.46–8.48%, with controlled invoice CPU support. Report regression
  debt remains unresolved.
- Shared immutable builtin definitions remove2,568 B from the builtin factory,
  25.06% of that factory, not whole execution. Local factory clocks are adverse;
  complete-script results are modest or inconclusive.
- Qualified XML settings selection removes intermediate match lists and UTF16 tag
  conversion: allocation falls44.83%, with elapsed about35.6% lower in confirmation.
  This is a source-specific comparison; [search-control uncertainty remains](../benchmarks/results/2026-11-13-xml-selection/README.md).
- Incremental XML permits early exit before constructing unused records. One
  622,640 B first-record lookup takes about0.081ms instead of25–26ms after whole-tree
  construction. [Both script algorithm and runtime differ](../benchmarks/results/2026-11-14-incremental-xml/README.md);
  this is not an interpreter or CPython speedup. Whole-document controls and
  conservative streaming-memory limitations remain explicit.
- Numeric while helpers: an identical4,096-row CSV validation/report script runs
  17–19.5%faster warm and14.8–16.8%faster compile-to-result, with wider CPU support.
  Allocation stays flat; [invoice control uncertainty and qualification status](../benchmarks/results/2026-11-14-native-backedges/README.md)
  remain explicit. This source-specific result does not change the CPython rows.
- Larger closed helpers reuse admitted bound storage: an identical4,096-row CSV
  fulfillment/pricing/report script allocates9.84%less and runs12.9–13.7%faster warm,
  14.6–16.1%faster compile-to-result, with wider CPU support. [Small-helper control
  uncertainty and qualification status](../benchmarks/results/2026-11-14-native-owned-slots/README.md)
  remain explicit; this is a separate source pair, not a CPython comparison.
- Shared integer chains reduce allocation10.45%for a complete4,096-row fulfillment
  script with arithmetic in an ordinary loop. Warm elapsed improves3–4%and compile-
  to-result1.4–2.7%, with wider CPU support. This is a modest CPU result; [variable
  helper controls and adverse text elapsed debt](../benchmarks/results/2026-11-14-shared-integer-chains/README.md)
  remain explicit. The CPython rows use the earlier qualified source above.

[Experimental history and source-specific evidence](../benchmarks/EXPERIMENTS.md)
preserve controlled comparisons, rejected designs, adverse results and qualification.
Do not add these percentages together or apply them to the CPython ratios.

## Reproducing measurements

Use the [existing comparison tools](../benchmarks/README.md) and
[test/package checks](../tests/README.md). Use short focused comparisons during
iteration and batch full platform, stress and installed-consumer qualification at
delivery checkpoints. Quiet-machine checks and output equality are required;
retain unstable controls and failed conditions. Publication remains postponed.
