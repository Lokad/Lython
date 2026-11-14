# Performance against CPython

| Representative workload, warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice / defensive pricing helper |4,096 records /127,031 B|7.545–7.590 ms|5.936–6.086 ms|1.24–1.28×|Diagnostic 3994; spreads 0.59/2.46%; max IQR 1.35%|
| Text / captured helper and counter|3,072 lines /35,840 B|1.298–1.315 ms|878.121–892.451 µs|1.45–1.50×|Earlier fa40; Diagnostic; spreads 1.34/1.61%; max IQR 2.30%|
| JSON-lines / aggregation helpers|4,096 records /110,592 B|3.397–3.499 ms|4.068–4.078 ms|0.83–0.86×|Earlier fa40; Diagnostic; spreads 2.92/0.24%; max IQR 1.16%|
| File search / defensive keyword-default helper |2,049 lines /42,010 B; stop after1,025|266.372–266.982 µs|160.576–162.048 µs|1.64–1.66×|Diagnostic 3994; spreads 0.23/0.91%; max IQR 2.60%|
| JSON-to-Markdown report|3,072 records /189,441 B|2.836–2.880 ms|2.332–2.340 ms|1.21–1.23×|Earlier fa40; Variable; spreads 1.56/0.32%; max IQR 4.82%|
| CSV fulfillment / pricing and validation|4,096 records /126,821 B|8.540–8.604 ms|6.652–6.783 ms|1.26–1.29×|Earlier fa40; Diagnostic; spreads 0.75/1.92%; max IQR 1.90%|
| CSV validation / for-loop checksum|4,096 records /49,163 B|5.441–5.579 ms|4.210–4.219 ms|1.29–1.33×|Earlier fa40; Diagnostic; spreads 2.47/0.21%; max IQR 1.30%|
| Configuration JSON edit |96 B|75.3–76.4 µs|59.6–59.7 µs|1.26–1.28×|Earlier cdd9; Diagnostic; spreads1.56/0.15%; maxIQR1.55%|

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** These are
complete embedding measurements, not isolated interpreter CPU. Spreads are the
Lython/CPython process-median differences; moving controls and IQRs remain visible.

Defensive invoice and file-search rows measured 2026-11-14 UTC at
**3994dfda / frozen b5a6426e**, against stock **CPython3.13.16**.
Full Windows/Ubuntu actual .NET11/10, stress and installed-consumer qualification
[passes at this source](../benchmarks/results/2026-11-14-protected-helper-activation/delivery/README.md).
[Exact scripts, all phases/raw CPU and controls](../benchmarks/results/2026-11-14-protected-cpython-checkpoint/README.md).
The other five main rows retain earlier **fa40d674 / frozen73a1b233** measurements,
also2026-11-14 and CPython3.13.16. Their full qualification
[passes](../benchmarks/results/2026-11-14-range-iteration-stage/delivery/README.md);
[B747](../benchmarks/results/2026-11-14-range-cpython-checkpoint/README.md)
preserves their exact sources and all adverse/variable observations.
The current defensive helpers add try/except behavior; do not attribute
cross-collection differences to a runtime change. No unchanged nine-job rerun.
Process spreads and IQRs limit magnitude precision; counter microbenchmarks do
not predict these complete workloads.
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
[Previous checkpoint and all adverse observations](../benchmarks/results/2026-11-14-iteration-cpython-checkpoint/README.md).

## Compilation, compile-to-result and startup

The current defensive invoice and earlier96 B configuration sources separate
frontend cost from execution:

| Phase | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| Defensive invoice compilation alone |103.845–105.523 µs|98.430–98.738 µs|1.05–1.07×|Diagnostic 3994; spreads 1.62/0.31%; max IQR 2.78%|
| Defensive invoice compile and execute once |7.729–7.743 ms|6.218–6.237 ms|1.24–1.25×|Diagnostic 3994; spreads 0.19/0.31%; max IQR 1.30%|
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
| CSV validation /5,120 records |4.170–4.249 ms|4.464–4.520 ms|0.92–0.95×|Earlier fa40; 2d91eb1a; diagnostic; other lanes partial|
| Ordinary text summary /3,072 lines |1.295–1.323 ms|1.290–1.308 ms|0.99–1.03×|983bab96; variable|
| Copy/checksum/manifest /4,107 B |0.384 ms|0.260 ms|1.47–1.48×|459813a3; diagnostic|
| Patch generation /two1,024-line files |0.556–0.558 ms|0.647–0.651 ms|0.85–0.86×|4aa2d46d; diagnostic|

Earlier warm text had12.40%Lython process spread; the CSV campaign's other lanes
lacked their final gate. Those inconclusive observations, replaced rows, exact
dates and source links remain in the [previous guide](../benchmarks/results/2026-11-13-fixed-definitions-cpython-checkpoint/prior-BENCHMARKS.md).
Cross-collection differences cannot attribute a gain to a particular optimization.

## Improvements within Lython

These source-pair results are separate from CPython and cannot be added together:

| Change / complete affected script | CPU/elapsed evidence | Allocation | Limits and exact evidence |
| --- | --- | ---: | --- |
| Typed builtin results /CSV fulfillment | Warm3–4%lower, compile-to-result2.6–5.6%lower, supporting CPU | −18.20% | Later bed6e57e versus3994/b5; full delivery pending. Invoice−5%allocation but+.94–1.87%elapsed/+2–4%CPU debt; text adverse/uncertain, short/observed controls variable. [Exact source and every control](../benchmarks/results/2026-11-14-builtin-scalar-publication/README.md). |
| Protected try/except helpers /invoice and file search | Invoice17–18.5%faster warm,16–19%faster compile-to-result; search41–43%faster, with CPU support | −29.19%/−74.65% | Later b5a6426e versus1c5/ff2; [full qualification passes](../benchmarks/results/2026-11-14-protected-helper-activation/delivery/README.md). Short calls+.5–3.4%, text uncertain. [Source and every control](../benchmarks/results/2026-11-14-protected-helper-activation/README.md). |
| Native primitive storage /CSV while-validator | Warm21–23%lower, compile-to-result21–22%lower, supportingCPU | −40.9% | Source-specific; short/zero helpers+4–10%, observed+2–4%, small JSONCPU debt. [Qualified evidence](../benchmarks/results/2026-11-14-native-primitive-storage/README.md). |
| Native integer workspace reuse /fulfillment | Warm mixed/modestly lower; fresh modestly lower | −14.57% | Allocation/ownership improvement; short calls+.6–2.9%. [Qualified source and debt](../benchmarks/results/2026-11-14-native-integer-workspace/delivery/README.md). |
| Direct UTF8 formatting /report | Controlled confirmation supports5–8%lower elapsed/CPU | −8.67% | Historical revision; original adverse15.125ms remains unexplained. [Confirmation and original conditions](../benchmarks/results/2026-11-02-formatting-confirmation/README.md). |

Native helper returns established about19–20%less allocation; their original
elapsed gains remain diagnostic. Other source pairs, rejected designs, range
microbenchmarks and qualification procedures remain in the
[experimental history](../benchmarks/EXPERIMENTS.md) and the
[previous guide](../benchmarks/results/2026-11-14-protected-helper-activation/prior-BENCHMARKS.md).
The external table above identifies current and earlier revisions separately;
source-pair improvements do not establish embedding latency.

## Reproducing measurements

Use the [existing comparison tools](../benchmarks/README.md) and
[test/package checks](../tests/README.md). Use short focused comparisons during
iteration and batch full platform, stress and installed-consumer qualification at
delivery checkpoints. Quiet-machine checks and output equality are required;
retain unstable controls and failed conditions. Publication remains postponed.
