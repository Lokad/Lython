# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice | 4,096 records / 127,031 B | 6.790–6.809 ms | 6.019–6.064 ms | 1.12–1.13× | Diagnostic; controls L/C 0.29/0.74%; IQR≤0.62% |
| Text normalization | 4,096 lines / 52,224 B | 1.125–1.140 ms | 0.508–0.564 ms | 2.00–2.24× | Diagnostic; controls L/C 1.31/10.85%; IQR≤1.32% |
| JSON-lines aggregation | 4,096 records / 110,592 B | 3.456–3.458 ms | 4.087–4.100 ms | 0.84–0.85× | Diagnostic; controls L/C 0.05/0.33%; IQR≤0.66% |
| File search / defensive helper | 2,049 lines / 42,010 B; stop at 1,025 | 0.275–0.283 ms | 0.171–0.176 ms | 1.56–1.65× | Diagnostic; controls L/C 2.79/2.98%; IQR≤1.15% |
| Configuration edit | 1,024 paths / 7,177 B | 0.179–0.182 ms | 0.185–0.185 ms | 0.96–0.98× | Earlier312f/0443; diagnostic; controls L/C 2.10/0.01%; IQR≤1.37% |
| Report rendering | 1,024 records / 189,441 B | 2.774–2.790 ms | 2.327–2.361 ms | 1.18–1.20× | Earlier312f/0443; diagnostic; controls L/C 0.57/1.46%; IQR≤2.04% |

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** These are
complete scripts inside warm processes, with two independent processes per engine.
Ranges are process-median combinations, not confidence intervals. Control movement
and sample IQRs remain visible; exact magnitudes are diagnostic. Controls L/C are
the percentage differences between the two Lython/CPython process medians.

Measured **2026-11-16 UTC**: the first four workloads use committed
**81f60210 / frozen a44f65a8** versus stock **CPython3.13.16**. Configuration/report
remain earlier **312f5911 / frozen0443b5cf** measurements, identified per row.
Quiet Azure F4as_v7 / Ubuntu24.04.4, four AMD EPYC9V45vCPUs,16GiB/no swap;
.NET11RC3 natural workstation GC/normal tiering, CPython GIL/GC. The fixed six-case
catalog36c0a528 is identical in every process within each collection. Two processes
per engine,5s full-request warmups,five rotated approximately0.5s samples; startup
is outside the clocks. Latest twelve jobs400.971s,all correctness/quiet/join/archive
gates pass. Normalization's **10.85%CPython control movement** makes its precise
warm ratio uncertain. No causal gain is inferred across source collections.
[Latest CSV/text/JSON/search evidence](../benchmarks/results/2026-11-16-consolidated-cpython-checkpoint/README.md),
[earlier configuration/report evidence](../benchmarks/results/2026-11-16-generated-scripts-cpython/README.md).
Tables describe named measured sources; later changes require their own evidence.

Warm includes synchronous Run, mediated RAM I/O, complete stdout/file goldens and
prior-result cleanup. It excludes compilation/startup, fixture reset/protocol and
final cache teardown. Lython's host performs capability/shared-budget checks;
CPython uses native buffered files without equivalent containment. These are useful
embedding comparisons, not isolated interpreter CPU or storage-device throughput.
No startup, pending-I/O, contention, heap/RSS or embedding-tail comparison is available.

## Compilation and newly generated scripts

| Workload | Phase | Lython | CPython | Ratio | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice | Compilation only | 0.099–0.100 ms | 0.094–0.094 ms | 1.06–1.06× | Diagnostic; controls L/C 0.19/0.23%; IQR≤1.27% |
| CSV invoice | Compile and execute once | 6.920–6.994 ms | 6.213–6.316 ms | 1.10–1.13× | Diagnostic; controls L/C 1.07/1.67%; IQR≤0.92% |
| Text normalization | Compilation only | 0.035–0.036 ms | 0.037–0.038 ms | 0.94–0.97× | Diagnostic; controls L/C 1.69/1.37%; IQR≤1.96% |
| Text normalization | Compile and execute once | 1.215–1.257 ms | 0.597–0.613 ms | 1.98–2.11× | Diagnostic; controls L/C 3.48/2.64%; IQR≤0.95% |
| JSON-lines aggregation | Compilation only | 0.054–0.054 ms | 0.053–0.053 ms | 1.02–1.03× | Diagnostic; controls L/C 0.39/0.34%; IQR≤4.14% |
| JSON-lines aggregation | Compile and execute once | 3.590–3.609 ms | 4.177–4.312 ms | 0.83–0.86× | Diagnostic; controls L/C 0.55/3.22%; IQR≤3.80% |
| File search / defensive helper | Compilation only | 0.031–0.031 ms | 0.036–0.036 ms | 0.85–0.86× | Diagnostic; controls L/C 0.16/1.14%; IQR≤1.05% |
| File search / defensive helper | Compile and execute once | 0.363–0.372 ms | 0.237–0.245 ms | 1.48–1.57× | Diagnostic; controls L/C 2.44/3.18%; IQR≤0.76% |
| Configuration edit | Compilation only | 0.036–0.036 ms | 0.035–0.035 ms | 1.04–1.05× | Earlier312f/0443; diagnostic; controls L/C 0.23/0.07%; IQR≤1.01% |
| Configuration edit | Compile and execute once | 0.240–0.245 ms | 0.255–0.255 ms | 0.94–0.96× | Earlier312f/0443; diagnostic; controls L/C 1.92/0.10%; IQR≤0.67% |
| Report rendering | Compilation only | 0.040–0.040 ms | 0.038–0.038 ms | 1.06–1.06× | Earlier312f/0443; diagnostic; controls L/C 0.27/0.32%; IQR≤0.53% |
| Report rendering | Compile and execute once | 2.974–2.994 ms | 2.456–2.475 ms | 1.20–1.22× | Earlier312f/0443; diagnostic; controls L/C 0.66/0.77%; IQR≤4.04% |

Compilation includes compiled-owner disposal; compile-to-result includes compilation,
one execution and compiled-owner disposal. Independently warmed phase medians are
not additive. Wider process CPU in the artifacts also includes reset/protocol.
Compilation is not the main gap in these measured scripts; configuration is near
CPython and report execution remains18–22%slower. Text normalization
retains a substantial execution gap. This does not establish the cause of that gap.

## Historical and unresolved measurements

The [preceding guide](../benchmarks/results/2026-11-16-representative-cpython-checkpoint/prior-BENCHMARKS.md)
preserves report, patch, configuration, captured text and async measurements with
their actual earlier revisions. They do not describe current HEAD. All original
controls and adverse cases remain linked; no source-change gain or regression
recovery is inferred across collections.

In particular, normalization CPython shifted about0.73→0.50ms in earlier same-case
collections despite identical worker/binary/flags/case bytes. Catalog initialization
differed, but its causal effect remains unisolated. This checkpoint fixes catalog
and initialization within its twelve jobs; it does not explain the earlier shift
or the latest10.85%CPython normalization-control movement.
Moving process controls remain uncertainty, not proof of a failed design.

## Improvements within Lython

Source-pair results are separate from CPython and cannot be added together:

| Change / affected workload | Separate source-pair evidence | Limits |
| --- | --- | --- |
| One native/CLR cursor lifetime / complete normalization | Final compile-to-result2.8–6.2%lower elapsed, calls1.6–2.8%lower, supporting CPU; allocation flat | Numeric elapsed1.1–6.7%adverse with moving controls/mixed CPU; compilation1–3%higher, CSV/report inconclusive. [Sources, refinement and controls](../benchmarks/results/2026-11-16-native-consumer-consolidation/README.md). |
| Compact native iteration / complete numeric squared-error report | Allocation about16%lower through both APIs; repeated warm elapsed/CPU favorable with uncertain magnitude | Call loop repeatedly about2–3%slower; normalization inconclusive with moving controls. [Measured revisions, phases and controls](../benchmarks/results/2026-11-16-compact-iteration-result/README.md). [Full delivery passes](../benchmarks/results/2026-11-16-compact-iteration-result/delivery/README.md). |
| One generator-expression engine / complete class-generator report | Allocation −21.09%; warm 8.1–9.4% lower elapsed, compile-to-result 4.1–7.2% lower, supporting CPU | Compilation 4–9% higher; earlier frame/cursor admission. Normalization control moves; calls elapsed about 1% higher without supporting CPU. [Source pair and controls](../benchmarks/results/2026-11-16-generator-consolidation/README.md). |
| Shared immutable subtext / complete normalization and JSON-lines | Allocation−19.8%/−11.3%; normalization warm1.8–4.6%lower elapsed, JSON2.9–5.6%lower, supporting CPU | Full backing stays charged for surviving views; fresh reference moves3.37%. [Evidence and full delivery](../benchmarks/results/2026-11-16-shared-subtext/README.md). |
| Shared line construction / normalization | Allocation−1.73%; warm3.8–8.5%lower elapsed, supporting CPU | Original failed idle gate and moving JSON control retained. [Evidence and full delivery](../benchmarks/results/2026-11-16-line-construction/confirmation/README.md). |
| Final-slot range transport / diagnostic module and generator drain | Allocation−97.8%/−13.7%; module24–26%lower elapsed through Run/27%through RunAsync; generator6–8%lower, supporting CPU | Native helper roughly flat; CSV inconclusive with3.58%moving controls. No broad file-script gain. [Refinement/original adversity](../benchmarks/results/2026-11-16-iteration-slot-transport/refined/README.md); [Full delivery passes](../benchmarks/results/2026-11-16-iteration-slot-transport/refined/delivery/README.md). |

Historical native helper returns establish19–20%less allocation; original elapsed
gains remain diagnostic. Direct UTF8 formatting establishes8.67%less allocation;
[controlled confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md)
supports5–8%lower report elapsed/CPU, while original adverse15.125ms remains unexplained.
Other improvements, rejected call-arena/controller designs and accepted regression
debts remain in [experimental history](../benchmarks/EXPERIMENTS.md). Historical
thresholds and one-attempt rules do not govern current work.

Use the [existing comparison tooling](../benchmarks/README.md) for reproduction;
detailed qualification and exact commands stay with each immutable result. No NuGet
publication follows from measurements or qualification.
