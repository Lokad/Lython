# Performance against CPython

| Representative workload, warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice / ordinary pricing helper | 4,096 records / 127,031 B | 7.358–7.414 ms | 6.087–6.100 ms | 1.21–1.22× | Diagnostic cf754; spreads 0.76/0.23%; max IQR 1.96% |
| Text / captured helper and counter | 3,072 lines / 35,840 B | 1.259–1.272 ms | 0.885–0.888 ms | 1.42–1.44× | Diagnostic cf754; spreads 1.04/0.33%; max IQR 0.60% |
| JSON-lines / aggregation helpers|4,096 records /110,592 B|3.397–3.499 ms|4.068–4.078 ms|0.83–0.86×|Earlier fa40; Diagnostic; spreads 2.92/0.24%; max IQR 1.16%|
| File search / defensive keyword-default helper |2,049 lines /42,010 B; stop after1,025|266.372–266.982 µs|160.576–162.048 µs|1.64–1.66×|Earlier3994; Diagnostic; spreads 0.23/0.91%; max IQR 2.60%|
| JSON-to-Markdown report|3,072 records /189,441 B|2.836–2.880 ms|2.332–2.340 ms|1.21–1.23×|Earlier fa40; Variable; spreads 1.56/0.32%; max IQR 4.82%|
| CSV fulfillment / pricing and validation | 4,096 records / 126,821 B | 8.017–8.121 ms | 6.721–6.777 ms | 1.18–1.21× | Diagnostic cf754; spreads 1.27/0.82%; max IQR 1.02% |
| CSV validation / for-loop checksum|4,096 records /49,163 B|5.441–5.579 ms|4.210–4.219 ms|1.29–1.33×|Earlier fa40; Diagnostic; spreads 2.47/0.21%; max IQR 1.30%|
| Configuration JSON edit |96 B|75.3–76.4 µs|59.6–59.7 µs|1.26–1.28×|Earlier cdd9; Diagnostic; spreads1.56/0.15%; maxIQR1.55%|

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** These are
complete embedding measurements, not isolated interpreter CPU. Spreads are the
Lython/CPython process-median differences; moving controls and IQRs remain visible.

Invoice, fulfillment and captured-text rows measured **2026-11-15 UTC** at
**cf754dc7 / frozen 95798eb9**, against stock **CPython 3.13.16**.
[Exact scripts, phases, raw CPU and controls](../benchmarks/results/2026-11-15-shared-slots-cpython-checkpoint/README.md).
[Validation status and exact source proof](../benchmarks/results/2026-11-15-shared-executable-slots/delivery/README.md).
These are diagnostic complete-script observations, not cross-collection causal gains. The
[earlier checkpoint](../benchmarks/results/2026-11-14-current-cpython-checkpoint/README.md)
keeps its adverse/variable observations. The
[shared-slot source pair](../benchmarks/results/2026-11-15-shared-executable-slots/README.md)
records modest invoice improvement and accepted observed-helper/possible
fulfillment regression; the external baseline does not erase those tradeoffs.
File search retains earlier **3994dfda / b5a6426e**, also2026-11-14 and fully
[qualified](../benchmarks/results/2026-11-14-protected-helper-activation/delivery/README.md);
[B757](../benchmarks/results/2026-11-14-protected-cpython-checkpoint/README.md) also
preserves its defensive-invoice source and phases. JSON/report/for-validator
retain earlier **fa40d674 / 73a1b233**; [qualified B747](../benchmarks/results/2026-11-14-range-cpython-checkpoint/README.md)
keeps their exact sources and every adverse/variable observation. These older
rows do not claim today's HEAD, and counter microbenchmarks do not predict them.
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

Current fulfillment and earlier defensive invoice/configuration sources separate
compilation from execution:

| Phase | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| Fulfillment compilation alone | 129.351–130.314 µs | 115.500–115.824 µs | 1.12–1.13× | Diagnostic cf754; spreads 0.74/0.28%; max IQR 1.52% |
| Fulfillment compile and execute once | 8.338–8.439 ms | 6.953–7.055 ms | 1.18–1.21× | Diagnostic cf754; spreads 1.19/1.46%; max IQR 1.58% |
| Defensive invoice compilation alone |103.845–105.523 µs|98.430–98.738 µs|1.05–1.07×|Earlier3994; Diagnostic; spreads 1.62/0.31%; max IQR 2.78%|
| Defensive invoice compile and execute once |7.729–7.743 ms|6.218–6.237 ms|1.24–1.25×|Earlier3994; Diagnostic; spreads 0.19/0.31%; max IQR 1.30%|
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
| Closed word segment /captured text | Warm2.9–5.7%lower elapsed/3.5–5.3%lower CPU | Flat | Retained6164a933 versus957; full qualification pending. Fresh magnitude inconclusive (8.77%reference spread), JSON+.8–2.7%elapsed unresolved/accepted; no universal gain. [All controls](../benchmarks/results/2026-11-15-closed-word-segment/README.md). |
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

## Reproducing measurements

Use the [existing comparison tools](../benchmarks/README.md) and
[test/package checks](../tests/README.md). Use short focused comparisons during
iteration and batch full platform, stress and installed-consumer qualification at
delivery checkpoints. Quiet-machine checks and output equality are required;
retain unstable controls and failed conditions.
