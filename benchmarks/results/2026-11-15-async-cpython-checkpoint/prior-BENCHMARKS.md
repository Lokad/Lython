# Performance against CPython

| Representative workload (warm unless stated) | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration/manifest / compile and execute once | 833 B inputs / 1,877 B source | 495.332–496.371 µs | 460.923–464.835 µs | 1.07–1.08× | Diagnostic 8103; spreads 0.21/0.85%; max IQR 1.46% |
| CSV invoice / ordinary pricing helper | 4,096 records / 127,031 B | 7.358–7.414 ms | 6.087–6.100 ms | 1.21–1.22× | Earlier cf754; diagnostic; spreads 0.76/0.23%; max IQR 1.96% |
| Text / captured helper and counter | 3,072 lines / 35,840 B | 1.220–1.267 ms | 0.903–0.921 ms | 1.32–1.40× | Variable a7ab; spreads 3.90/2.02%; max IQR 3.22% |
| JSON-lines / aggregation helpers | 4,096 records / 110,592 B | 3.454–3.481 ms | 4.071–4.100 ms | 0.84–0.85× | Diagnostic a7ab; spreads 0.79/0.70%; max IQR 1.15% |
| File search / defensive keyword-default helper | 2,049 lines / 42,010 B; stop after 1,025 | 0.267–0.284 ms | 0.151–0.152 ms | 1.76–1.88× | Variable a7ab; spreads 5.86/0.84%; max IQR 2.31% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 2.772–2.840 ms | 2.308–2.319 ms | 1.20–1.23× | Variable a7ab; spreads 2.44/0.45%; max IQR 6.24% |
| CSV fulfillment / pricing and validation | 4,096 records / 126,821 B | 8.067–8.184 ms | 6.736–6.793 ms | 1.19–1.22× | Diagnostic a7ab; spreads 1.43/0.84%; max IQR 0.94% |
| CSV validation / for-loop checksum|4,096 records /49,163 B|5.441–5.579 ms|4.210–4.219 ms|1.29–1.33×|Earlier fa40; Diagnostic; spreads 2.47/0.21%; max IQR 1.30%|
| Configuration/manifest / warm | 833 B inputs / 1,877 B source | 191.507–192.335 µs | 154.809–155.556 µs | 1.23–1.24× | Diagnostic 8103; spreads 0.43/0.48%; max IQR 1.87% |

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** These are
complete embedding measurements, not isolated interpreter CPU. Spreads are the
Lython/CPython process-median differences; moving controls and IQRs remain visible.

The six-helper configuration/manifest script measured **2026-11-15 UTC** at
**810379be / frozen 8fbfa3f0** against stock **CPython 3.13.16**. It reads configuration
and dependency declarations, normalizes targets and labels, updates configuration
and writes a manifest. [Source, independent goldens, phases and raw controls](../benchmarks/results/2026-11-15-config-manifest-phases/README.md).
This source passes [full platform, original stress and installed-package checks](../benchmarks/results/2026-11-15-function-slot-binding/delivery/README.md).
The one-shot ratio includes compilation and compiled-owner disposal; it is not the
sum of the separately warmed phase medians. No startup or cross-source gain claim.

Fulfillment, captured text, JSON-lines, file search and report rows measured
**2026-11-15 UTC** at **a7ab41f5 / frozen 716176a8**, against stock **CPython
3.13.16**. [Exact current checkpoint, controls and raw evidence](../benchmarks/results/2026-11-15-retained-cpython-checkpoint/README.md).
This runtime passes [full Windows/Ubuntu .NET 11/10, original stress and installed
Release-consumer qualification](../benchmarks/results/2026-11-15-csv-final-payload/delivery/README.md).
File search and captured text have moving Lython controls; report has a 6.24%
maximum IQR. Keep their ranges visible rather than treating every row as stable.
The checkpoint is diagnostic and does not attribute changes from older collections.

Invoice retains **cf754dc7 /95798eb9**; for-validator **fa40d674 /73a1b233**;
configuration **cdd92944 /f4c4f027**, with their named dates, phases and qualification
in the [preceding guide](../benchmarks/results/2026-11-15-retained-cpython-checkpoint/prior-BENCHMARKS.md).
These older rows do not claim today's HEAD. The
[preceding text/JSON checkpoint](../benchmarks/results/2026-11-15-word-segment-cpython-checkpoint/README.md)
and all earlier adverse results remain preserved. Retained Lython source-pair
regressions remain separate from CPython comparisons.
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

Current helper-rich configuration, earlier fulfillment and defensive invoice/configuration
sources separate compilation from execution:

| Phase | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| Configuration/manifest compilation alone | 202.568–202.781 µs | 156.855–157.823 µs | 1.28–1.29× | Diagnostic 8103; spreads 0.11/0.62%; max IQR 1.26% |
| Fulfillment compilation alone | 129.617–130.770 µs | 114.740–115.506 µs | 1.12–1.14× | Diagnostic a7ab; spreads 0.88/0.67%; max IQR 1.07% |
| Fulfillment compile and execute once | 8.223–8.254 ms | 6.990–7.120 ms | 1.15–1.18× | Diagnostic a7ab; spreads 0.38/1.82%; max IQR 1.12% |
| Defensive invoice compilation alone |103.845–105.523 µs|98.430–98.738 µs|1.05–1.07×|Earlier3994; Diagnostic; spreads 1.62/0.31%; max IQR 2.78%|
| Defensive invoice compile and execute once |7.729–7.743 ms|6.218–6.237 ms|1.24–1.25×|Earlier3994; Diagnostic; spreads 0.19/0.31%; max IQR 1.30%|
| Earlier 96 B configuration compilation alone |36.6–37.3 µs|34.3–34.4 µs|1.06–1.09×|Diagnostic; spreads1.94/0.17%; maxIQR1.97%|
| Earlier 96 B configuration compile and execute once |136.5–137.3 µs|121.4–124.2 µs|1.10–1.13×|Diagnostic; spreads0.61/2.27%; maxIQR3.65%|
| Process startup |—|—|—|No current comparison|

Compile-to-result includes compilation and compiled-owner disposal, plus warm
execution costs above. Wider process CPU includes fixture reset/protocol.
Startup, shared-budget contention, genuinely pending I/O, RSS/heap peaks and
embedding tail latency remain unmeasured. The
[previous fully qualified checkpoint](../benchmarks/results/2026-11-13-fixed-definitions-cpython-checkpoint/README.md)
retains its own exact revision, boundaries and adverse/variable observations.

At 810379be, `RunAsync` with completed host I/O measured about 3–7.5% slower than
`Run` on CSV, 9.5–12.5% on captured text and 3–6.8% on file search. This is an API
comparison, separate from CPython; it does not measure pending-I/O latency.
[Controls, included cleanup and remaining attribution](../benchmarks/results/2026-11-15-whole-script-hosting/README.md).

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
| Scheduled async invocation lifetime / file search | Wider process CPU 10.8–12.9% lower; elapsed 0.4–4.0% lower | No reduction claimed | Retained f304b3db versus 8fbf; [full qualification passes](../benchmarks/results/2026-11-15-invocation-lifetime/delivery/README.md). Text remains uncertain despite a bounded shared-hash control; all original adversity and sync-control debt retained. [Source pair and limits](../benchmarks/results/2026-11-15-invocation-lifetime/README.md). |
| Shared CSV payload / metadata-heavy validated records | Warm slightly lower; fresh and ordinary controls mixed | −5.56% | Retained 716176a8 versus 6164; allocation improvement; [full qualification passes](../benchmarks/results/2026-11-15-csv-final-payload/delivery/README.md). [Exact fixture, controls and limits](../benchmarks/results/2026-11-15-csv-final-payload/README.md). |
| Closed word segment / captured text | Warm 2.9–5.7% lower elapsed / 3.5–5.3% lower CPU | Flat | Retained 6164a933 versus 957; [full qualification passes](../benchmarks/results/2026-11-15-closed-word-segment/delivery/README.md). Fresh magnitude inconclusive (8.77% reference spread), JSON +0.8–2.7% elapsed unresolved/accepted; no universal gain. [All controls](../benchmarks/results/2026-11-15-closed-word-segment/README.md). |
| Typed builtin results /CSV fulfillment | Warm3–4%lower, compile-to-result2.6–5.6%lower, supporting CPU | −18.20% | Later bed6e57e versus3994/b5; [full delivery passes](../benchmarks/results/2026-11-14-builtin-scalar-publication/delivery/README.md). Invoice−5%allocation but+.94–1.87%elapsed/+2–4%CPU debt; text adverse/uncertain, short/observed controls variable. [Exact source and every control](../benchmarks/results/2026-11-14-builtin-scalar-publication/README.md). |
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

[Explicit 0.11 retention tradeoffs](../benchmarks/results/2026-11-15-runtime-roadmap-review/README.md)
keep historical regressions visible; later source pairs do not prove their recovery.

## Reproducing measurements

Use the [existing comparison tools](../benchmarks/README.md) and
[test/package checks](../tests/README.md). Use short focused comparisons during
iteration and batch full platform, stress and installed-consumer qualification at
delivery checkpoints. Quiet-machine checks and output equality are required;
retain unstable controls and failed conditions.
