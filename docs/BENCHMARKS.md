# Performance against CPython

| Representative workload / phase | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Report / format method / warm Run | 1,024 records / 189,441 B JSON | 4.364–4.403 ms | 2.614–2.633 ms | 1.66–1.68× | Diagnostic; spreads 0.89/0.75%; IQR ≤4.49% |
| Report / format method / compilation only | 1,024 records / 189,441 B JSON | 0.037–0.037 ms | 0.036–0.036 ms | 1.02–1.03× | Diagnostic; spreads 0.05/0.95%; IQR ≤0.94% |
| Report / format method / compile and execute once | 1,024 records / 189,441 B JSON | 4.438–4.479 ms | 2.727–2.771 ms | 1.60–1.64× | Diagnostic; spreads 0.92/1.62%; IQR ≤4.15% |
| Report / f-string / warm Run | 1,024 records / 189,441 B JSON | 2.789–2.803 ms | 2.366–2.384 ms | 1.17–1.18× | Diagnostic; spreads 0.51/0.72%; IQR ≤4.63% |
| CSV invoice / warm Run | 4,096 records / 127,031 B | 6.637–6.728 ms | 6.087–6.144 ms | 1.08–1.11× | Diagnostic; spreads 1.35/0.94%; IQR ≤1.52% |
| CSV invoice / compilation only | 4,096 records / 127,031 B | 0.100–0.101 ms | 0.094–0.094 ms | 1.07–1.08× | Diagnostic; spreads 0.67/0.14%; IQR ≤1.06% |
| CSV invoice / compile and execute once | 4,096 records / 127,031 B | 6.883–6.972 ms | 6.249–6.276 ms | 1.10–1.12× | Diagnostic; spreads 1.29/0.44%; IQR ≤1.50% |
| CSV fulfillment / warm Run | 4,096 records / 126,821 B | 7.568–7.594 ms | 6.783–6.794 ms | 1.11–1.12× | Diagnostic; spreads 0.34/0.17%; IQR ≤1.70% |
| Text / captured helper and counter / warm Run | 3,072 lines / 35,840 B | 1.205–1.255 ms | 0.885–0.898 ms | 1.34–1.42× | Diagnostic; spreads 4.14/1.37%; IQR ≤1.73% |
| Text / captured helper and counter / compile and execute once | 3,072 lines / 35,840 B | 1.267–1.301 ms | 1.034–1.042 ms | 1.22–1.26× | Diagnostic; spreads 2.67/0.74%; IQR ≤1.04% |
| Text / inline / warm Run | 3,072 lines / 35,840 B | 0.818–0.851 ms | 0.802–0.804 ms | 1.02–1.06× | Diagnostic; spreads 3.91/0.21%; IQR ≤1.71% |
| JSON-lines / aggregation helpers / warm Run | 4,096 records / 110,592 B | 3.467–3.563 ms | 4.098–4.117 ms | 0.84–0.87× | Diagnostic; spreads 2.77/0.47%; IQR ≤1.59% |
| File search / defensive helper / warm Run | 2,049 lines / 42,010 B; stop after 1,025 | 0.272–0.273 ms | 0.152–0.155 ms | 1.76–1.80× | Diagnostic; spreads 0.51/1.74%; IQR ≤2.91% |
| Configuration / six-helper manifest / compilation only | 833 B inputs / 1,877 B source | 0.198–0.200 ms | 0.155–0.158 ms | 1.25–1.29× | Diagnostic; spreads 1.02/1.92%; IQR ≤1.96% |
| Configuration / six-helper manifest / compile and execute once | 833 B inputs / 1,877 B source | 0.497–0.498 ms | 0.463–0.470 ms | 1.06–1.08× | Diagnostic; spreads 0.04/1.49%; IQR ≤1.68% |

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** These are
complete embedding measurements, not isolated interpreter CPU. Spreads compare
the two Lython/CPython process medians; IQRs and moving controls stay visible.

**Report rows:58eeea6f/frozen9d071eba**; [affected checkpoint](../benchmarks/results/2026-11-15-layout-cpython-checkpoint/README.md)
passes all measurement/cleanup gates. Full runtime qualification is active at capture.
Method formatting remains1.66–1.68×CPython warm; compilation is1.02–1.03×.
The [preceding report checkpoint](../benchmarks/results/2026-11-15-format-cpython-checkpoint/README.md)
retains the2.04–2.07×warm result and noisy8.86%fresh CPython IQR atb06b8593.

Measured **2026-11-15 UTC**, stock **CPython3.13.16**, quiet Azure F4as_v7 /
Ubuntu24.04.4, four AMD EPYC9V45 vCPUs,16GiB/no swap,.NET11RC3 workstation
GC/normal tiering and CPython GIL/GC. **CSV invoice, captured warm text, JSON,
file search and configuration:2865f619/frozen55ba4c8a**;
[eight-phase checkpoint](../benchmarks/results/2026-11-15-store-cpython-checkpoint/README.md)
passes all measurement/cleanup gates; [full runtime qualification passes](../benchmarks/results/2026-11-15-prepared-store-targets/delivery/README.md).
**Captured fresh and inline text:1b82cd46/frozen0edc0c1c**, with
[measurement](../benchmarks/results/2026-11-15-cased-cpython-checkpoint/README.md)
and [full runtime qualification](../benchmarks/results/2026-11-15-fused-cased-preparation/delivery/README.md).
**CSV fulfillment:43bce13a/frozen9e890d18**, with
[measurement](../benchmarks/results/2026-11-15-arithmetic-cpython-checkpoint/README.md)
and [full qualification](../benchmarks/results/2026-11-15-completed-arithmetic/delivery/README.md).
Earlier archive-floor/staging failures and all original controls remain preserved.
The current captured-text control moves4.14%; this limits exact gap claims. These
cross-collection rows do not establish source-change speedups. Captured text also
updates a nonlocal counter; its difference from inline text does not isolate calls.

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
| Reused method-format layout / complete report | Warm elapsed17.4–19.3%lower; compile-to-result18.2–19.8%lower, supporting CPU | −18.47% | Retained9d071eba/8afb; moving f-string controls/CSVup to2.8%slower and conservative retained layout charges remain. [Source pair/limits](../benchmarks/results/2026-11-15-method-format-layout/README.md); full delivery pending. |
| Final-output field sink / method-format report | Warm elapsed5.8–9.4%lower; compile-to-result7.8–9.0%lower, supporting CPU | −17.31% | Retained8afb/55ba; moving f-string/CSV controls and earlier conservative metadata denial remain. str.format now awaits hosted callbacks. [Source pair and limits](../benchmarks/results/2026-11-15-formatting-final-sink/README.md); [Full delivery passes](../benchmarks/results/2026-11-15-formatting-final-sink/delivery/README.md). |
| Fused cased preparation / direct and captured text | Direct elapsed4.0–7.7%lower/CPU2.6–7.9%; captured compile-to-result2.1–5.4%lower/CPU2.7–4.7% | Flat(+128Btext) | Retained0ed/9e; captured warm uncertain/earlier adverse, JSON widerCPU0–3.3%higher. Scratch4×/6×input can deny earlier. [Decision/all controls](../benchmarks/results/2026-11-15-fused-cased-preparation/confirmation/README.md). [Full qualification passes](../benchmarks/results/2026-11-15-fused-cased-preparation/delivery/README.md). |
| Shared completed arithmetic / invoice | Elapsed2.3–3.8% lower / CPU2.1–3.3% | Flat | Retained43bce13a/9e89; fresh widerCPU+0.4–3.1% and original adverse/moving controls remain. [Source pair and confirmation](../benchmarks/results/2026-11-15-completed-arithmetic/confirmation/README.md). [Full delivery passes](../benchmarks/results/2026-11-15-completed-arithmetic/delivery/README.md); no isolated causal or universal gain. |
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
