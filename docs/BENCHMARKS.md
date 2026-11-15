# Performance against CPython

| Representative workload / phase | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice / warm Run | 4,096 records / 127,031 B | 6.710–6.749 ms | 5.983–6.077 ms | 1.10–1.13× | Diagnostic; spreads 0.57/1.55%; IQR ≤1.10% |
| CSV invoice / compilation only | 4,096 records / 127,031 B | 0.101–0.106 ms | 0.094–0.094 ms | 1.08–1.13× | Diagnostic; spreads 4.52/0.49%; IQR ≤1.74% |
| CSV invoice / compile and execute once | 4,096 records / 127,031 B | 6.826–7.004 ms | 6.215–6.263 ms | 1.09–1.13× | Diagnostic; spreads 2.54/0.76%; IQR ≤2.10% |
| CSV fulfillment / warm Run | 4,096 records / 126,821 B | 7.568–7.594 ms | 6.783–6.794 ms | 1.11–1.12× | Diagnostic; spreads 0.34/0.17%; IQR ≤1.70% |
| Text / captured helper and counter / warm Run | 3,072 lines / 35,840 B | 1.211–1.248 ms | 0.888–0.891 ms | 1.36–1.41× | Diagnostic; spreads 3.05/0.31%; IQR ≤1.24% |
| JSON-lines / aggregation helpers / warm Run | 4,096 records / 110,592 B | 3.416–3.484 ms | 4.053–4.070 ms | 0.84–0.86× | Diagnostic; spreads 1.98/0.42%; IQR ≤1.31% |
| File search / defensive helper / warm Run | 2,049 lines / 42,010 B; stop after 1,025 | 0.286–0.303 ms | 0.157–0.158 ms | 1.81–1.93× | Diagnostic; spreads 5.90/1.10%; IQR ≤3.18% |
| Configuration / six-helper manifest / compile and execute once | 833 B inputs / 1,877 B source | 0.498–0.498 ms | 0.470–0.475 ms | 1.05–1.06× | Diagnostic; spreads 0.08/1.02%; IQR ≤1.40% |

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** These are
complete embedding measurements, not isolated interpreter CPU. Spreads compare
the two Lython/CPython process medians; IQRs and moving controls stay visible.

Measured **2026-11-15 UTC**, committed **43bce13a / frozen9e890d18**, stock
**CPython3.13.16**, quiet Azure F4as_v7 / Ubuntu24.04.4, four AMD EPYC9V45 vCPUs,
16GiB/no swap,.NET11RC3 workstation GC/normal tiering and CPython GIL/GC.
Full runtime qualification is active in43737584665. [All source identities,
controls, boundaries and original failures](../benchmarks/results/2026-11-15-arithmetic-cpython-checkpoint/README.md).
The measurement owner completes sampling and cleanup, then fails its archive disk
floor; persistent evidence is recovered and independently verified. That failure
remains explicit. Compilation/text/search controls vary; no exact portable gap or
cross-collection speedup is claimed.

Warm includes synchronous Run, mediated RAM I/O, stdout/file goldens and prior-result
cleanup; excludes compilation/startup, fixture reset/protocol and final cache teardown.
Compilation-only includes compiled-owner disposal; compile-to-result includes
compilation, one execution and compiled-owner disposal. Separate phase medians
are not additive. Wider CPU also includes reset/protocol. The host completes I/O
synchronously and performs capability/shared-budget checks; CPython uses native
buffered files without equivalent containment. No startup, pending-I/O, contention,
RSS/heap or embedding-tail comparison is available.

## Historical and unresolved cases

The [preceding guide](../benchmarks/results/2026-11-15-arithmetic-cpython-checkpoint/prior-BENCHMARKS.md)
retains earlier report, validation, patch, ZIP, configuration and async rows with
actual revisions, phases and limitations. They do not describe today's HEAD.
RunAsync text/search atb58e/f304 remain1.37–1.46×/1.78–1.88× with moving controls;
those hosts complete I/O synchronously. The earlier report row ata7ab/7161 is
1.20–1.23× with6.24%maximum IQR. Other partial/inconclusive lanes and all adverse
observations remain linked; no recovery is inferred from later measurements.

## Improvements within Lython

These source-pair results are separate from CPython and cannot be added together:

| Change / complete affected script | CPU/elapsed evidence | Allocation | Limits and exact evidence |
| --- | --- | ---: | --- |
| Shared completed arithmetic / invoice | Elapsed2.3–3.8% lower / CPU2.1–3.3% | Flat | Retained43bce13a/9e89; fresh widerCPU+0.4–3.1% and original adverse/moving controls remain. [Source pair and confirmation](../benchmarks/results/2026-11-15-completed-arithmetic/confirmation/README.md). Full delivery active; no isolated causal or universal gain. |
| Final-slot integer tuples / invoice and fulfillment | Invoice elapsed 8.5–10.1% lower / CPU 6.4–10.8%; fulfillment elapsed 3.8–5.0% lower / CPU 2.7–4.6% | −8.56% / −5.32% | Retained 22e5b72c versus f304. Original text regressions and refined moving controls remain explicit; no universal gain. [Source pairs and limits](../benchmarks/results/2026-11-15-tuple-operand-prototype/refined/README.md). |
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
