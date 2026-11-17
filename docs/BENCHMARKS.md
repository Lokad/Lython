# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Floating JSON numeric report | 4,096 records / 56,495 B | 1.739–1.740 ms | 0.829–0.830 ms | 2.09–2.10× | R471; diagnostic; controls L/C 0.07/0.22%; IQR≤1.79% |
| Text normalization through a helper | 4,096 lines / 52,224 B | 1.236–1.250 ms | 0.872–0.905 ms | 1.37–1.43× | Diagnostic; controls L/C 1.15/3.76%; IQR≤2.39% |
| Report rendering through a helper | 3,072 records / 189,441 B | 2.655–2.685 ms | 2.200–2.220 ms | 1.20–1.22× | Diagnostic; controls L/C 1.13/0.92%; IQR≤4.68% |
| CSV invoice | 4,096 records / 127,031 B | 6.844–6.963 ms | 6.064–6.114 ms | 1.12–1.15× | Earlier checkpoint; diagnostic; controls L/C 1.74/0.83%; IQR≤1.62% |
| Text normalization | 4,096 lines / 52,224 B | 1.041–1.049 ms | 0.504–0.504 ms | 2.06–2.08× | Earlier checkpoint; diagnostic; controls L/C 0.76/0.09%; IQR≤2.12% |
| JSON-lines aggregation | 4,096 records / 110,592 B | 3.504–3.579 ms | 4.069–4.080 ms | 0.86–0.88× | Earlier checkpoint; diagnostic; controls L/C 2.16/0.27%; IQR≤1.11% |
| File search / defensive helper | 2,049 lines / 42,010 B; stop at 1,025 | 0.259–0.264 ms | 0.160–0.161 ms | 1.60–1.65× | Earlier checkpoint; diagnostic; controls L/C 2.25/0.87%; IQR≤1.10% |
| Configuration edit | 1,024 paths / 7,177 B | 0.179–0.182 ms | 0.185–0.185 ms | 0.96–0.98× | Earlier 312f/0443; diagnostic; controls L/C 2.10/0.01%; IQR≤1.37% |
| Report rendering | 3,072 records / 189,441 B | 2.774–2.790 ms | 2.327–2.361 ms | 1.18–1.20× | Earlier 312f/0443; diagnostic; controls L/C 0.57/1.46%; IQR≤2.04% |

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** These are
complete scripts inside warm processes, with two independent processes per engine.
Ranges are process-median combinations, not confidence intervals. Control movement
and sample IQRs remain visible; exact magnitudes are diagnostic. Controls L/C are
the percentage differences between the two Lython/CPython process medians.

Measured **2026-11-17 UTC**: floating report uses **cb7472e7 + R471 refinement /
frozen5ecb2599**, stock **CPython3.13.16**; runtime delivery qualification pending.
Its three separated phases finish104.54s with all gates passed; same machine and
5s warmup boundaries below. [Floating baseline](../benchmarks/results/2026-11-17-float-transport/cpython/README.md).
The two helper workloads use committed
**ccf20ba3 / frozen7a599347** versus stock **CPython3.13.16**. CSV,generator
normalization,JSON and search remain earlier **46343804 / frozen aaef443f**;
configuration/report remain **312f5911 / frozen0443b5cf**. All rows name a measured
source; they do not automatically describe current HEAD. Helper scripts use
explicit loops and functions,not the earlier generator/streaming shapes.
Quiet Azure F4as_v7 / Ubuntu24.04.4,four AMD EPYC9V45vCPUs,16GiB/no swap;
.NET11RC3 natural workstation GC/normal tiering,CPythonGIL/GC. Two processes per
engine,5s full-request warmups,five rotated roughly0.5s samples;startup excluded.
Latest six jobs203.8s,all correctness/quiet/join/archive gates pass. Warm helper
text CPython controls move3.76%,report IQR≤4.68%;fresh Lython controls3.65/3.30%.
Earlier fresh CSV/normalization controls also move. Preserve those uncertainties.
No causal source gain across collections or additive phase subtraction.
[Latest helper baseline](../benchmarks/results/2026-11-17-receiver-cpython-checkpoint/README.md),
[earlier CSV/text/JSON/search checkpoint](../benchmarks/results/2026-11-17-sequence-cpython-checkpoint/README.md),
[older configuration/report evidence](../benchmarks/results/2026-11-16-generated-scripts-cpython/README.md).

Warm includes synchronous Run, mediated RAM I/O, complete stdout/file goldens and
prior-result cleanup. It excludes compilation/startup, fixture reset/protocol and
final cache teardown. Lython's host performs capability/shared-budget checks;
CPython uses native buffered files without equivalent containment. These are useful
embedding comparisons, not isolated interpreter CPU or storage-device throughput.
No startup, pending-I/O, contention, heap/RSS or embedding-tail comparison is available.

## Compilation and newly generated scripts

| Workload | Phase | Lython | CPython | Ratio | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Floating JSON numeric report | Compilation only | 0.057–0.058 ms | 0.061–0.061 ms | 0.93–0.95× | R471; diagnostic; controls L/C 1.95/0.26%; IQR≤0.52% |
| Floating JSON numeric report | Compile and execute once | 1.841–1.872 ms | 0.956–0.957 ms | 1.92–1.96× | R471; diagnostic; controls L/C 1.66/0.03%; IQR≤0.79% |
| Text normalization through a helper | Compilation only | 0.047–0.047 ms | 0.044–0.044 ms | 1.05–1.06× | Diagnostic; controls L/C 0.36/0.24%; IQR≤1.01% |
| Text normalization through a helper | Compile and execute once | 1.345–1.394 ms | 0.795–0.802 ms | 1.68–1.75× | Diagnostic; controls L/C 3.65/0.82%; IQR≤1.93% |
| Report rendering through a helper | Compilation only | 0.053–0.054 ms | 0.049–0.049 ms | 1.08–1.10× | Diagnostic; controls L/C 1.69/0.19%; IQR≤0.74% |
| Report rendering through a helper | Compile and execute once | 2.850–2.944 ms | 2.432–2.481 ms | 1.15–1.21× | Diagnostic; controls L/C 3.30/2.03%; IQR≤4.68% |
| CSV invoice | Compilation only | 0.100–0.101 ms | 0.093–0.094 ms | 1.06–1.08× | Earlier checkpoint; diagnostic; controls L/C 0.62/1.03%; IQR≤0.96% |
| CSV invoice | Compile and execute once | 7.078–7.560 ms | 6.289–6.324 ms | 1.12–1.20× | Earlier checkpoint; diagnostic; controls L/C 6.81/0.55%; IQR≤0.68% |
| Text normalization | Compilation only | 0.036–0.036 ms | 0.037–0.037 ms | 0.95–0.96× | Earlier checkpoint; diagnostic; controls L/C 0.80/0.08%; IQR≤0.79% |
| Text normalization | Compile and execute once | 1.132–1.175 ms | 0.631–0.686 ms | 1.65–1.86× | Earlier checkpoint; diagnostic; controls L/C 3.86/8.75%; IQR≤1.64% |
| JSON-lines aggregation | Compilation only | 0.050–0.051 ms | 0.051–0.051 ms | 0.99–1.01× | Earlier checkpoint; diagnostic; controls L/C 1.98/0.13%; IQR≤0.53% |
| JSON-lines aggregation | Compile and execute once | 3.610–3.725 ms | 4.193–4.197 ms | 0.86–0.89× | Earlier checkpoint; diagnostic; controls L/C 3.17/0.10%; IQR≤0.47% |
| File search / defensive helper | Compilation only | 0.031–0.031 ms | 0.036–0.036 ms | 0.85–0.85× | Earlier checkpoint; diagnostic; controls L/C 0.44/0.02%; IQR≤1.41% |
| File search / defensive helper | Compile and execute once | 0.342–0.352 ms | 0.225–0.227 ms | 1.50–1.56× | Earlier checkpoint; diagnostic; controls L/C 2.94/0.95%; IQR≤1.10% |
| Configuration edit | Compilation only | 0.036–0.036 ms | 0.035–0.035 ms | 1.04–1.05× | Earlier 312f/0443; diagnostic; controls L/C 0.23/0.07%; IQR≤1.01% |
| Configuration edit | Compile and execute once | 0.240–0.245 ms | 0.255–0.255 ms | 0.94–0.96× | Earlier 312f/0443; diagnostic; controls L/C 1.92/0.10%; IQR≤0.67% |
| Report rendering | Compilation only | 0.040–0.040 ms | 0.038–0.038 ms | 1.06–1.06× | Earlier 312f/0443; diagnostic; controls L/C 0.27/0.32%; IQR≤0.53% |
| Report rendering | Compile and execute once | 2.974–2.994 ms | 2.456–2.475 ms | 1.20–1.22× | Earlier 312f/0443; diagnostic; controls L/C 0.66/0.77%; IQR≤4.04% |

Compilation includes compiled-owner disposal; compile-to-result includes compilation,
one execution and compiled-owner disposal. Independently warmed phase medians are
not additive. Wider process CPU in the artifacts also includes reset/protocol.
Compilation is not the main gap in these measured scripts; configuration is near
CPython. The latest helper-text compile-to-result ratio exceeds its warm ratio. CPython
also has a lower fresh median than warm here; the ratio alone does not establish
a Lython setup defect. Attribute first-use/cleanup and measurement effects before
selecting a frontend redesign. The
latest helper report remains about15–22%slower across these boundaries. Earlier
measurements retain their named sources and uncertainty.

## Historical and unresolved measurements

The [preceding guide](../benchmarks/results/2026-11-16-representative-cpython-checkpoint/prior-BENCHMARKS.md)
preserves report, patch, configuration, captured text and async measurements with
their actual earlier revisions. They do not describe current HEAD. All original
controls and adverse cases remain linked; no source-change gain or regression
recovery is inferred across collections.

Earlier generator normalization CPython shifted about0.73→0.50ms despite identical
worker/binary/flags/case bytes. Catalog initialization differed,but its causal
effect remains unisolated. Earlier warm/fresh controls also move; the latest two
helper cases do not explain those earlier conditions. Preserve the original
observations rather than inferring recovery across collections.

A [same-process phase diagnostic](../benchmarks/results/2026-11-17-helper-phase-attribution/README.md)
does not reproduce the helper-text inversion: CPython warm 0.715–0.720 ms, fresh
0.833–0.840 ms; Lython warm 1.255–1.265 ms, fresh 1.353–1.354 ms. Its alternating
phase conditions differ from the table. The earlier cause remains unresolved;
no Lython setup defect or performance gain is established. Report controls move.

The historical report input label is corrected to3,072records: its archived size
parameter1024generated three rows per unit. Timing values are unchanged.
[Decoded fixture proof](../benchmarks/results/2026-11-17-receiver-cpython-checkpoint/historical-report-input-proof.json).

## Improvements within Lython

Source-pair results are separate from CPython and cannot be added together:

| Change / affected workload | Separate source-pair evidence | Limits |
| --- | --- | --- |
| Floating arithmetic-to-iteration transport / complete JSON numeric report | Warm 23–26% lower elapsed, fresh 23–27% lower, with supporting CPU; allocation −28.52%, accounted peak unchanged | Integer control inconclusive with 7–9% movement; earlier confirmed integer 2–5% regression is not proven resolved. [Refinement, original adversity and checks](../benchmarks/results/2026-11-17-float-transport/README.md). Focused checks pass; full delivery qualification pending. |
| Shared receiver calls / complete helper-list text and report scripts | Warm text37–40%lower elapsed,report24–25%;fresh37–39%/22–24%,with supporting CPU | Allocation−51.78%/−33.16%;retained peaks essentially flat. Call/CSVcontrols roughly flat,numeric magnitude uncertain. [Exact source pair,workloads and checks](../benchmarks/results/2026-11-17-receiver-calls/README.md). Combined platform/stress/package qualification passes; no inferred CPython ratio. |
| Tab-expansion construction / complete normalization | Warm7.2–7.7%lower elapsed and6.8–11.4%lower compile-to-result,with supporting CPU | Physical allocation+0.27%;accounted peak+4KiB. Calls/CSVroughly flat,fresh candidate controls3.49%. [Exact source pair and checks](../benchmarks/results/2026-11-17-tab-expansion/README.md). Qualified with the receiver milestone; no inferred CPython ratio. |
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
