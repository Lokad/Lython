# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice | 4,096 records / 127,031 B | 6.844–6.963 ms | 6.064–6.114 ms | 1.12–1.15× | Diagnostic; controls L/C 1.74/0.83%; IQR≤1.62% |
| Text normalization | 4,096 lines / 52,224 B | 1.041–1.049 ms | 0.504–0.504 ms | 2.06–2.08× | Diagnostic; controls L/C 0.76/0.09%; IQR≤2.12% |
| JSON-lines aggregation | 4,096 records / 110,592 B | 3.504–3.579 ms | 4.069–4.080 ms | 0.86–0.88× | Diagnostic; controls L/C 2.16/0.27%; IQR≤1.11% |
| File search / defensive helper | 2,049 lines / 42,010 B; stop at 1,025 | 0.259–0.264 ms | 0.160–0.161 ms | 1.60–1.65× | Diagnostic; controls L/C 2.25/0.87%; IQR≤1.10% |
| Configuration edit | 1,024 paths / 7,177 B | 0.179–0.182 ms | 0.185–0.185 ms | 0.96–0.98× | Earlier 312f/0443; diagnostic; controls L/C 2.10/0.01%; IQR≤1.37% |
| Report rendering | 1,024 records / 189,441 B | 2.774–2.790 ms | 2.327–2.361 ms | 1.18–1.20× | Earlier 312f/0443; diagnostic; controls L/C 0.57/1.46%; IQR≤2.04% |

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** These are
complete scripts inside warm processes, with two independent processes per engine.
Ranges are process-median combinations, not confidence intervals. Control movement
and sample IQRs remain visible; exact magnitudes are diagnostic. Controls L/C are
the percentage differences between the two Lython/CPython process medians.

Measured **2026-11-17 UTC**: the first four workloads use production committed
**46343804 / frozen aaef443f** versus stock **CPython 3.13.16**. Commit9677d8f7
changes accounting assertions/evidence only; full delivery is tracked separately.
Configuration/report remain earlier **312f5911 / frozen0443b5cf** measurements.
Quiet Azure F4as_v7 / Ubuntu24.04.4, four AMD EPYC9V45 vCPUs,16 GiB/no swap;
.NET11 RC3 natural workstation GC/normal tiering, CPython GIL/GC. Identical six-case
catalog36c0a528 in every process. Two processes per engine,5 s full-request warmups,
five rotated roughly0.5 s samples; startup excluded. Twelve jobs400.0 s,all
correctness/quiet/join/archive gates pass. Fresh CSV Lython controls move6.81%;
fresh normalization Lython/CPython move 3.86/8.75%. Retain that uncertainty;
warm normalization still shows about a 2.07× gap. No causal gain across collections.
[Current CSV/text/JSON/search evidence](../benchmarks/results/2026-11-17-sequence-cpython-checkpoint/README.md),
[previous checkpoint](../benchmarks/results/2026-11-16-consolidated-cpython-checkpoint/README.md),
[earlier configuration/report evidence](../benchmarks/results/2026-11-16-generated-scripts-cpython/README.md).
The tab-expansion change below is later source-pair evidence; these CPython
ratios exclude it. Tables describe named sources,not automatically current HEAD.

Warm includes synchronous Run, mediated RAM I/O, complete stdout/file goldens and
prior-result cleanup. It excludes compilation/startup, fixture reset/protocol and
final cache teardown. Lython's host performs capability/shared-budget checks;
CPython uses native buffered files without equivalent containment. These are useful
embedding comparisons, not isolated interpreter CPU or storage-device throughput.
No startup, pending-I/O, contention, heap/RSS or embedding-tail comparison is available.

## Compilation and newly generated scripts

| Workload | Phase | Lython | CPython | Ratio | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice | Compilation only | 0.100–0.101 ms | 0.093–0.094 ms | 1.06–1.08× | Diagnostic; controls L/C 0.62/1.03%; IQR≤0.96% |
| CSV invoice | Compile and execute once | 7.078–7.560 ms | 6.289–6.324 ms | 1.12–1.20× | Diagnostic; controls L/C 6.81/0.55%; IQR≤0.68% |
| Text normalization | Compilation only | 0.036–0.036 ms | 0.037–0.037 ms | 0.95–0.96× | Diagnostic; controls L/C 0.80/0.08%; IQR≤0.79% |
| Text normalization | Compile and execute once | 1.132–1.175 ms | 0.631–0.686 ms | 1.65–1.86× | Diagnostic; controls L/C 3.86/8.75%; IQR≤1.64% |
| JSON-lines aggregation | Compilation only | 0.050–0.051 ms | 0.051–0.051 ms | 0.99–1.01× | Diagnostic; controls L/C 1.98/0.13%; IQR≤0.53% |
| JSON-lines aggregation | Compile and execute once | 3.610–3.725 ms | 4.193–4.197 ms | 0.86–0.89× | Diagnostic; controls L/C 3.17/0.10%; IQR≤0.47% |
| File search / defensive helper | Compilation only | 0.031–0.031 ms | 0.036–0.036 ms | 0.85–0.85× | Diagnostic; controls L/C 0.44/0.02%; IQR≤1.41% |
| File search / defensive helper | Compile and execute once | 0.342–0.352 ms | 0.225–0.227 ms | 1.50–1.56× | Diagnostic; controls L/C 2.94/0.95%; IQR≤1.10% |
| Configuration edit | Compilation only | 0.036–0.036 ms | 0.035–0.035 ms | 1.04–1.05× | Earlier 312f/0443; diagnostic; controls L/C 0.23/0.07%; IQR≤1.01% |
| Configuration edit | Compile and execute once | 0.240–0.245 ms | 0.255–0.255 ms | 0.94–0.96× | Earlier 312f/0443; diagnostic; controls L/C 1.92/0.10%; IQR≤0.67% |
| Report rendering | Compilation only | 0.040–0.040 ms | 0.038–0.038 ms | 1.06–1.06× | Earlier 312f/0443; diagnostic; controls L/C 0.27/0.32%; IQR≤0.53% |
| Report rendering | Compile and execute once | 2.974–2.994 ms | 2.456–2.475 ms | 1.20–1.22× | Earlier 312f/0443; diagnostic; controls L/C 0.66/0.77%; IQR≤4.04% |

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
or the preceding10.85%CPython warm normalization-control movement. Current fresh
controls also move (CSV Lython6.81%,normalization CPython8.75%); preserved in full.
Moving process controls remain uncertainty, not proof of a failed design.

## Improvements within Lython

Source-pair results are separate from CPython and cannot be added together:

| Change / affected workload | Separate source-pair evidence | Limits |
| --- | --- | --- |
| Tab-expansion construction / complete normalization | Warm7.2–7.7%lower elapsed and6.8–11.4%lower compile-to-result,with supporting CPU | Physical allocation+0.27%;accounted peak+4KiB. Calls/CSVroughly flat,fresh candidate controls3.49%. [Exact source pair and checks](../benchmarks/results/2026-11-17-tab-expansion/README.md). Full delivery pending; no inferred CPython ratio. |
| Shared native sequence construction / complete normalization and tuple-report | Warm normalization9–12%lower elapsed, tuple report2–5%lower/warm and about4.7–4.8%lower compile-to-result, with supporting CPU; generator-list diagnostic also favorable | Allocation essentially flat; report accounted peak12%higher. Numeric control uncertain with moving references; Run CSVroughly flat. Actual RunAsync report/generator medians favorable with moving controls; CSV0.1–3.6%adverse. [Exact sources, original adversity and checks](../benchmarks/results/2026-11-17-sequence-construction/README.md). |
| Shared string/byte replacement / complete normalization | Bounded unchanged replacements reuse their input; changing-data allocation essentially flat; no CPU gain established | Binary elapsed1–1.5%higher, text mixed, moving call/CPU controls. Retained for consolidation and bounded no-op behavior. [Original adversity and refinement](../benchmarks/results/2026-11-16-bulk-replacement/refined/README.md). |
| Shared scalar-index reads / complete numeric report | Allocation −18.99%; warm and confirmed compile-to-result elapsed/CPU favor candidate, magnitude uncertain | Reference controls move 5.70/6.83%; original fresh pairings include +3.02% adversity. Calls/text inconclusive. [Sources, original condition and confirmation](../benchmarks/results/2026-11-16-scalar-index-transport/README.md). |
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
