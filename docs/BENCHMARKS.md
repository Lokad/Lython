# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice | 4,096 records / 127,031 B | 6.637–6.697 ms | 6.090–6.173 ms | 1.08–1.10× | Diagnostic; controls L/C 0.91/1.35%; IQR≤1.41% |
| Text normalization | 4,096 lines / 52,224 B | 1.116–1.151 ms | 0.500–0.505 ms | 2.21–2.30× | Diagnostic; controls L/C 3.12/0.84%; IQR≤1.60% |
| JSON-lines aggregation | 4,096 records / 110,592 B | 3.451–3.453 ms | 4.078–4.101 ms | 0.84–0.85× | Diagnostic; controls L/C 0.03/0.56%; IQR≤0.90% |
| File search / defensive helper | 2,049 lines / 42,010 B; stop at 1,025 | 0.284–0.287 ms | 0.159–0.161 ms | 1.76–1.80× | Diagnostic; controls L/C 0.97/1.31%; IQR≤2.19% |
| Configuration edit | 1,024 paths / 7,177 B | 0.179–0.182 ms | 0.185–0.185 ms | 0.96–0.98× | Diagnostic; controls L/C 2.10/0.01%; IQR≤1.37% |
| Report rendering | 1,024 records / 189,441 B | 2.774–2.790 ms | 2.327–2.361 ms | 1.18–1.20× | Diagnostic; controls L/C 0.57/1.46%; IQR≤2.04% |

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** These are
complete scripts inside warm processes, with two independent processes per engine.
Ranges are process-median combinations, not confidence intervals. Control movement
and sample IQRs remain visible; exact magnitudes are diagnostic. Controls L/C are
the percentage differences between the two Lython/CPython process medians.

Measured **2026-11-16 UTC**, Lython **312f5911 / frozen0443b5cf** versus stock
**CPython3.13.16**. Quiet Azure F4as_v7 / Ubuntu24.04.4, four AMD EPYC9V45 vCPUs,
16GiB/no swap; .NET11RC3 natural workstation GC/normal tiering, CPython GIL/GC.
The first four rows use the fixed four-case R437catalog43b7c6b0; the configuration/
report rows use R444catalog36c0a528with all six cases. Every engine/control within
each collection loads identical catalog bytes; initialization/startup stay outside
the clocks. Three bounded six-phase collections use5s full-request warmups and
five rotated approximately0.5s rounds. All image/golden/quiet/join/archive gates
pass. [CSV/text/JSON/search evidence](../benchmarks/results/2026-11-16-representative-cpython-checkpoint/README.md),
[configuration/report evidence](../benchmarks/results/2026-11-16-generated-scripts-cpython/README.md).
Different collection initialization is explicit; no causal source-change gain is inferred.
These tables describe the named measured revision, not today's HEAD. Later
source-pair measurements appear separately below.

Warm includes synchronous Run, mediated RAM I/O, complete stdout/file goldens and
prior-result cleanup. It excludes compilation/startup, fixture reset/protocol and
final cache teardown. Lython's host performs capability/shared-budget checks;
CPython uses native buffered files without equivalent containment. These are useful
embedding comparisons, not isolated interpreter CPU or storage-device throughput.
No startup, pending-I/O, contention, heap/RSS or embedding-tail comparison is available.

## Compilation and newly generated scripts

| Workload | Phase | Lython | CPython | Ratio | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV invoice | Compilation only | 0.099–0.100 ms | 0.093–0.094 ms | 1.06–1.07× | Diagnostic; controls L/C 0.08/0.64%; IQR≤1.62% |
| CSV invoice | Compile and execute once | 6.890–6.925 ms | 6.216–6.255 ms | 1.10–1.11× | Diagnostic; controls L/C 0.50/0.63%; IQR≤1.83% |
| Text normalization | Compilation only | 0.036–0.036 ms | 0.037–0.037 ms | 0.95–0.96× | Diagnostic; controls L/C 0.15/0.65%; IQR≤1.65% |
| Text normalization | Compile and execute once | 1.211–1.252 ms | 0.593–0.614 ms | 1.97–2.11× | Diagnostic; controls L/C 3.31/3.58%; IQR≤2.15% |
| JSON-lines aggregation | Compilation only | 0.051–0.052 ms | 0.051–0.051 ms | 0.99–1.02× | Diagnostic; controls L/C 2.26/0.40%; IQR≤1.68% |
| JSON-lines aggregation | Compile and execute once | 3.553–3.576 ms | 4.209–4.225 ms | 0.84–0.85× | Diagnostic; controls L/C 0.64/0.37%; IQR≤1.92% |
| File search / defensive helper | Compilation only | 0.031–0.031 ms | 0.036–0.036 ms | ~0.85× | Diagnostic; controls L/C 0.87/0.14%; IQR≤1.68% |
| File search / defensive helper | Compile and execute once | 0.355–0.380 ms | 0.221–0.228 ms | 1.56–1.72× | Diagnostic; controls L/C 6.88/3.30%; IQR≤2.93% |
| Configuration edit | Compilation only | 0.036–0.036 ms | 0.035–0.035 ms | 1.04–1.05× | Diagnostic; controls L/C 0.23/0.07%; IQR≤1.01% |
| Configuration edit | Compile and execute once | 0.240–0.245 ms | 0.255–0.255 ms | 0.94–0.96× | Diagnostic; controls L/C 1.92/0.10%; IQR≤0.67% |
| Report rendering | Compilation only | 0.040–0.040 ms | 0.038–0.038 ms | 1.06–1.06× | Diagnostic; controls L/C 0.27/0.32%; IQR≤0.53% |
| Report rendering | Compile and execute once | 2.974–2.994 ms | 2.456–2.475 ms | 1.20–1.22× | Diagnostic; controls L/C 0.66/0.77%; IQR≤4.04% |

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
and initialization within its twelve jobs; it does not explain the earlier shift.
Moving process controls remain uncertainty, not proof of a failed design.

## Improvements within Lython

Source-pair results are separate from CPython and cannot be added together:

| Change / affected workload | Separate source-pair evidence | Limits |
| --- | --- | --- |
| Compact native iteration / complete numeric squared-error report | Allocation about16%lower through both APIs; repeated warm elapsed/CPU favorable with uncertain magnitude | Call loop repeatedly about2–3%slower; normalization inconclusive with moving controls. [Measured revisions, phases and controls](../benchmarks/results/2026-11-16-compact-iteration-result/README.md). |
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
