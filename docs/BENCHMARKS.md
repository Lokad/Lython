# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| JSON-lines record transform | 4,096 records / 110,592 B | 3.898–4.012 ms | 4.388–4.423 ms | 0.88–0.91× | Diagnostic ea8/cf; controls L/C 2.93/0.79%; IQR≤0.79% |
| Numeric CSV quality report | 4,096 records / 127,031 B | 4.066–4.166 ms | 4.574–4.693 ms | 0.87–0.91× | Diagnostic; controls L/C 2.46/2.60%; IQR≤1.47% |
| Text normalization | 4,096 lines / 52,224 B | 0.916–0.925 ms | 0.503–0.520 ms | 1.76–1.84× | Diagnostic; controls L/C 0.99/3.37%; IQR≤0.98% |
| JSON-lines aggregation with a helper | 4,096 records / 110,592 B | 3.548–3.613 ms | 4.077–4.102 ms | 0.86–0.89× | Diagnostic; controls L/C 1.85/0.62%; IQR≤0.39% |
| Floating JSON numeric report | 4,096 pairs / 56,495 B | 1.582–1.621 ms | 0.820–0.821 ms | 1.93–1.98× | Diagnostic; controls L/C 2.48/0.14%; IQR≤1.09% |

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** Ranges cover
the combinations of two process medians per engine, not confidence intervals.
Controls L/C report the difference between the two Lython/CPython medians. These
complete scripts show substantial text/numeric gaps alongside faster CSV/JSON-lines.

The CSV/text/aggregation/floating rows were measured **2026-11-17 UTC**, committed
**0d2a6ab7 / frozen8d26963c**, stock
**CPython3.13.16** (PGO/LTO, GIL enabled, no JIT), quiet **AzureF4as_v7**,4vCPU
AMD EPYC9V45/16GiB, Ubuntu24.04.4, .NET11RC3. Existing workers/catalog;12jobs,
447.28seconds, rotated6second full-request warmups/five rounds, natural GC/tiering.
All actual-image/golden/quiet/exit/join gates pass. Exact magnitudes remain
diagnostic; these measurements do not describe an unmeasured later HEAD.
[Sources, raw observations and procedures](../benchmarks/results/2026-11-17-current-cpython-checkpoint/README.md).

The record-transform row is a separate **2026-11-17** checkpoint of qualified
**ea8e9f91 production / frozencf847936**, on the same machine/toolchains/boundaries:
three jobs116.40s. Its2.93%warm/3.31%fresh Lython control spread limits precision.
[Source, controls and raw data](../benchmarks/results/2026-11-17-record-cpython-baseline/README.md).

Warm clocks include Run, mediated RAM-backed file I/O, stdout/file golden checks
and previous-result cleanup. Lython's host supplies efficient range/append I/O;
CPython uses its native file APIs without Lython's containment/ownership checks.
Fixture reset, request framing, process startup and final cache teardown are outside
guest clocks. Wider process CPU includes reset/protocol and is preserved separately.
This is useful workload latency, not equivalent containment or cold startup.

## Compilation and compile-to-result

| Workload | Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| JSON-lines record transform | Compilation only | 0.057–0.058 ms | 0.056–0.056 ms | 1.01–1.02× | Diagnostic; controls L/C 1.68/0.08%; IQR≤1.81% |
| JSON-lines record transform | Compile-to-result | 4.085–4.220 ms | 4.517–4.546 ms | 0.90–0.93× | Diagnostic; controls L/C 3.31/0.63%; IQR≤1.11% |
| Numeric CSV quality report | Compilation only | 0.056–0.056 ms | 0.044–0.045 ms | 1.26–1.26× | Diagnostic; controls L/C 0.41/0.23%; IQR≤1.26% |
| Numeric CSV quality report | Compile and execute once | 4.220–4.315 ms | 4.613–4.681 ms | 0.90–0.94× | Diagnostic; controls L/C 2.25/1.47%; IQR≤0.79% |
| Text normalization | Compilation only | 0.036–0.036 ms | 0.037–0.037 ms | 0.96–0.96× | Diagnostic; controls L/C 0.42/0.44%; IQR≤0.48% |
| Text normalization | Compile and execute once | 0.985–0.989 ms | 0.592–0.593 ms | 1.66–1.67× | Diagnostic; controls L/C 0.42/0.20%; IQR≤0.76% |
| JSON-lines aggregation with a helper | Compilation only | 0.054–0.054 ms | 0.054–0.054 ms | 0.99–1.00× | Diagnostic; controls L/C 0.63/0.11%; IQR≤0.91% |
| JSON-lines aggregation with a helper | Compile and execute once | 3.642–3.707 ms | 4.192–4.194 ms | 0.87–0.88× | Diagnostic; controls L/C 1.78/0.05%; IQR≤0.45% |
| Floating JSON numeric report | Compilation only | 0.057–0.059 ms | 0.062–0.062 ms | 0.92–0.95× | Diagnostic; controls L/C 2.25/0.36%; IQR≤3.26% |
| Floating JSON numeric report | Compile and execute once | 1.718–1.800 ms | 1.012–1.019 ms | 1.69–1.78× | Diagnostic; controls L/C 4.78/0.68%; IQR≤0.34% |

Compilation includes compiled-owner disposal. Compile-to-result includes compilation,
one execution and compiled-owner disposal. Separately warmed medians are not
additive. Compilation is a small part of these scripts' elapsed costs. Floating
compile-to-result's4.78%Lython control movement limits precision; no unchanged retiming
is needed to establish the remaining large gap. Process startup was not measured.

## Uncertainty and earlier measurements

The floating fresh control, text warm CPython control and other distributions stay
visible above. The [previous guide](../benchmarks/results/2026-11-17-current-cpython-checkpoint/prior-BENCHMARKS.md)
preserves earlier helper-text/report, configuration, file-search and CSV invoice
measurements with their named revisions, original adverse controls and unresolved
phase inversions. Those results are historical, not this checkpoint or today's HEAD.
The new ratios do not establish a source-change gain across collections.

The same production source is [fully qualified at242ca284](../benchmarks/results/2026-11-17-returned-loan-cleanup/delivery/README.md)
on Windows/Ubuntu actual11/10, originalstress and installed Release consumers.
The earlier Windows10 fixture failure and correction remain linked there. This
does not imply binary equality with measured8d26963c or turn diagnostic ratios
into universal performance claims.

## Improvements within Lython

These are separate source-pair observations; they cannot be added or substituted
for the CPython ratios above.

| Change | Established outcome | Limits / evidence |
| --- | --- | --- |
| Generator field facts | Floating report warm9–10% lower/fresh12–14% lower elapsed with supporting CPU; allocation/peaks flat | Separate5ae019db versuscf847936 pair; text/call controls remain uncertain. [Evidence](../benchmarks/results/2026-11-17-generator-field-facts/README.md), [full qualification](../benchmarks/results/2026-11-17-generator-field-facts/delivery/README.md). |
| Reuse constant record keys | JSON record-transform allocation14.67% lower; warm elapsed12–14% lower/fresh9–12% lower with supporting CPU | Separate R499/cf847936 versus4b975a44/5d9633a8 pair; do not add independent medians. Text/JSON controls broadly flat; floating uncertain. [Evidence](../benchmarks/results/2026-11-17-constant-record-keys/README.md), [full qualification](../benchmarks/results/2026-11-17-constant-record-keys/delivery/README.md). |
| Final-slot helper container construction | JSON record-transform allocation39.54% lower, warm elapsed15–22% lower/fresh16–18% lower, supporting CPU | Named4b975a44/5d9633a8 versus0d2a6ab7/8d26963c; floating controls remain uncertain. [Source pair](../benchmarks/results/2026-11-17-function-container-construction/final/README.md), [full qualification](../benchmarks/results/2026-11-17-function-container-construction/delivery/README.md). |
| Native helper returns | Allocation about19–20% lower; qualified | Original elapsed improvements diagnostic; no universal CPU claim. [History](../benchmarks/EXPERIMENTS.md). |
| Direct UTF-8 formatting/storage | Allocation8.67% lower; controlled report elapsed/CPU5–8% lower supports retention | Original adverse15.125ms remains unexplained. [Original/confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md). |
| Floating arithmetic-to-iteration transport | Warm/fresh elapsed23–27% lower with supporting CPU; allocation28.52% lower | Historical source pair; integer controls uncertain, original regression retained. [Evidence](../benchmarks/results/2026-11-17-float-transport/README.md). |
| Shared receiver calls | Helper text/report warm24–40% lower, fresh22–39% lower, supporting CPU | Historical workloads/source pair; numeric magnitude uncertain. [Evidence](../benchmarks/results/2026-11-17-receiver-calls/README.md). |
| Bulk CSV/shared-key/final-row construction | Earlier complete-script6–8% improvement is already integrated | Not new pending work. Detailed other improvements/debts remain in the [previous guide](../benchmarks/results/2026-11-17-current-cpython-checkpoint/prior-BENCHMARKS.md) and [history](../benchmarks/EXPERIMENTS.md). |

Rejected optimizations and qualification narratives stay behind the evidence links.
Use the [existing tooling](../benchmarks/README.md); keep current benchmark status
and source revision here. Measurements and qualification do not authorize publication.
