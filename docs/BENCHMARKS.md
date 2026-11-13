# Performance against CPython

| Representative workload, warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice / pricing helpers |4,096 records /127,031 B|7.596–7.646 ms|6.012–6.069 ms|1.25–1.27×|Complete diagnostic; controls0.66/0.95%|
| Text / captured helper and counter |3,072 lines /35,840 B|1.438–1.442 ms|0.886–0.900 ms|1.60–1.63×|Complete diagnostic; controls0.24/1.55%|
| JSON-lines / aggregation helpers |4,096 records /110,592 B|3.424–3.473 ms|4.023–4.089 ms|0.84–0.86×|Complete, variable; maxIQR3.24%|
| File search / keyword-default helper |2,049 lines /42,010 B; stop after1,025|252.4–255.2 µs|148.6–149.8 µs|1.68–1.72×|Complete diagnostic; controls1.10/0.84%|
| JSON-to-Markdown report |3,072 records /189,441 B|2.829–2.876 ms|2.309–2.347 ms|1.21–1.25×|Complete, variable; maxIQR5.63%|

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** Text and
search retain substantial gaps. JSON is faster in this workload; report timings
are more variable. These embedding measurements do not isolate interpreter CPU.

Measured 2026-11-13 UTC: **runtime cb8736ff / frozen 5a184a1d**, test correction 90de765d,
against stock **CPython 3.13.16**. This source has full Windows/Ubuntu .NET 11/10,
stress and installed-package qualification. Later revisions and positional ZIP
work are excluded. The quiet Azure F4as_v7 VM runs
Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap and .NET 11 RC3, with ordinary
workstation GC/tiering and CPython GIL/GC enabled.

Warm clocks include mediated RAM-file I/O, full stdout/file validation and
prior-result cleanup. They exclude compilation, startup, fixture staging/reset,
protocol and final cache teardown. Two processes per engine, five rotating rounds;
time ranges span their medians and ratios include all four pairings, not confidence
intervals. Controls are the Lython/CPython process-median spreads. JSON/report
variability remains visible rather than being treated as a failed design.
[Exact sources, wider CPU, raw data and boundaries](../benchmarks/results/2026-11-13-fixed-definitions-cpython-checkpoint/README.md).

## Compilation, compile-to-result and startup

The same invoice script and measured revision separate frontend cost from execution:

| Phase | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| Compilation alone |102.4–103.9 µs|93.34–93.65 µs|1.09–1.11×|Complete diagnostic; maxIQR2.42%|
| Compile and execute once in a running host |7.686–7.806 ms|6.310–6.365 ms|1.21–1.24×|Complete diagnostic; maxIQR2.59%|
| Process startup |—|—|—|No current qualified comparison|

Compile-to-result includes compilation and compiled-owner disposal, plus the warm
execution costs above. Wider process CPU also includes fixture reset/protocol.
Startup, shared-budget contention, genuinely pending I/O, RSS/heap peaks and
embedding tail latency remain unmeasured at this checkpoint.

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

- Native helper returns: historical whole-run allocation falls19.92%Run/19.31%Async;
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

[Experimental history and source-specific evidence](../benchmarks/EXPERIMENTS.md)
preserve controlled comparisons, rejected designs, adverse results and qualification.
Do not add these percentages together or apply them to the CPython ratios.

## Reproducing measurements

Use the [existing comparison tools](../benchmarks/README.md) and
[test/package checks](../tests/README.md). Use short focused comparisons during
iteration and batch full platform, stress and installed-consumer qualification at
delivery checkpoints. Quiet-machine checks and output equality are required;
retain unstable controls and failed conditions. Publication remains postponed.
