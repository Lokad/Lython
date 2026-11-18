# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration edit | 181 B / 16 paths | 0.077–0.078 ms | 0.061–0.062 ms | 1.25–1.26× | Diagnostic627/5ae; controls L/C 0.78/0.75%; IQR≤0.99% |
| Formatted report | 3,072 records / 189,441 B | 2.934–2.942 ms | 2.671–2.737 ms | 1.07–1.10× | Diagnostic627/5ae; controls L/C 0.27/2.47%; IQR≤5.34% |
| JSON-lines record transform | 4,096 records / 110,592 B | 3.898–4.012 ms | 4.388–4.423 ms | 0.88–0.91× | Diagnostic ea8/cf; controls L/C 2.93/0.79%; IQR≤0.79% |
| Numeric CSV quality report | 4,096 records / 127,031 B | 4.066–4.166 ms | 4.574–4.693 ms | 0.87–0.91× | Diagnostic; controls L/C 2.46/2.60%; IQR≤1.47% |
| Text normalization | 4,096 lines / 52,224 B | 0.916–0.925 ms | 0.503–0.520 ms | 1.76–1.84× | Diagnostic; controls L/C 0.99/3.37%; IQR≤0.98% |
| JSON-lines aggregation with a helper | 4,096 records / 110,592 B | 3.548–3.613 ms | 4.077–4.102 ms | 0.86–0.89× | Diagnostic; controls L/C 1.85/0.62%; IQR≤0.39% |
| Floating JSON numeric report | 4,096 pairs / 56,495 B | 1.450–1.530 ms | 0.872–0.874 ms | 1.66–1.75× | Diagnostic627/5ae; controls L/C 5.49/0.29%; IQR≤0.90% |

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** Ranges cover
the combinations of two process medians per engine, not confidence intervals.
Controls L/C report the difference between the two Lython/CPython medians. These
complete scripts show substantial text/numeric gaps alongside faster CSV/JSON-lines.

The CSV/text/aggregation rows and floating compilation-only row were measured **2026-11-17 UTC**, committed
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

The configuration/report/floating execution rows are a **2026-11-18** checkpoint
of qualified **62786960 production / frozen5ae019db**, same machine/toolchains:
six jobs226.58s. Config controls are small; report CPython IQR5.34% and floating
Lython control spread5.49%warm/8.33%fresh limit precision. The remaining floating
gap is clear; exact source gains are not inferred across collections.
[Sources, controls and raw data](../benchmarks/results/2026-11-18-agent-cpython-checkpoint/README.md).

Warm clocks include Run, mediated RAM-backed file I/O, stdout/file golden checks
and previous-result cleanup. Lython's host supplies efficient range/append I/O;
CPython uses its native file APIs without Lython's containment/ownership checks.
Fixture reset, request framing, process startup and final cache teardown are outside
guest clocks. Wider process CPU includes reset/protocol and is preserved separately.
This is useful workload latency, not equivalent containment or cold startup.

## Compilation and compile-to-result

| Workload | Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration edit | Compilation only | 0.036–0.037 ms | 0.035–0.035 ms | 1.05–1.08× | Diagnostic627/5ae; controls L/C 3.34/0.05%; IQR≤0.70% |
| Configuration edit | Compile-to-result | 0.145–0.145 ms | 0.134–0.135 ms | 1.08–1.08× | Diagnostic627/5ae; controls L/C 0.09/0.32%; IQR≤0.85% |
| JSON-lines record transform | Compilation only | 0.057–0.058 ms | 0.056–0.056 ms | 1.01–1.02× | Diagnostic; controls L/C 1.68/0.08%; IQR≤1.81% |
| JSON-lines record transform | Compile-to-result | 4.085–4.220 ms | 4.517–4.546 ms | 0.90–0.93× | Diagnostic; controls L/C 3.31/0.63%; IQR≤1.11% |
| Numeric CSV quality report | Compilation only | 0.056–0.056 ms | 0.044–0.045 ms | 1.26–1.26× | Diagnostic; controls L/C 0.41/0.23%; IQR≤1.26% |
| Numeric CSV quality report | Compile and execute once | 4.220–4.315 ms | 4.613–4.681 ms | 0.90–0.94× | Diagnostic; controls L/C 2.25/1.47%; IQR≤0.79% |
| Text normalization | Compilation only | 0.036–0.036 ms | 0.037–0.037 ms | 0.96–0.96× | Diagnostic; controls L/C 0.42/0.44%; IQR≤0.48% |
| Text normalization | Compile and execute once | 0.985–0.989 ms | 0.592–0.593 ms | 1.66–1.67× | Diagnostic; controls L/C 0.42/0.20%; IQR≤0.76% |
| JSON-lines aggregation with a helper | Compilation only | 0.054–0.054 ms | 0.054–0.054 ms | 0.99–1.00× | Diagnostic; controls L/C 0.63/0.11%; IQR≤0.91% |
| JSON-lines aggregation with a helper | Compile and execute once | 3.642–3.707 ms | 4.192–4.194 ms | 0.87–0.88× | Diagnostic; controls L/C 1.78/0.05%; IQR≤0.45% |
| Floating JSON numeric report | Compilation only | 0.057–0.059 ms | 0.062–0.062 ms | 0.92–0.95× | Diagnostic; controls L/C 2.25/0.36%; IQR≤3.26% |
| Floating JSON numeric report | Compile-to-result | 1.518–1.645 ms | 1.027–1.030 ms | 1.47–1.60× | Diagnostic627/5ae; controls L/C 8.33/0.28%; IQR≤1.08% |

Compilation includes compiled-owner disposal. Compile-to-result includes compilation,
one execution and compiled-owner disposal. Separately warmed medians are not
additive. Warm-only numbers do not describe single-use scripts; use the separate
compile-to-result observations. Floating
compile-to-result's8.33%Lython control movement limits precision; the original4.78%
condition remains in the earlier checkpoint. No broad unchanged retiming is needed
to establish the remaining gap. Process startup was not measured.

## Uncertainty and earlier measurements

The floating process controls, report IQR, text warm CPython control and other distributions stay
visible above. The [previous guide](../benchmarks/results/2026-11-17-current-cpython-checkpoint/prior-BENCHMARKS.md)
preserves earlier helper-text/report, configuration, file-search and CSV invoice
measurements with their named revisions, original adverse controls and unresolved
phase inversions. Those results are historical, not this checkpoint or today's HEAD.
The new ratios do not establish a source-change gain across collections.

The older0d production source is [fully qualified at242ca284](../benchmarks/results/2026-11-17-returned-loan-cleanup/delivery/README.md);
the latest execution rows use [qualified62786960](../benchmarks/results/2026-11-17-generator-field-facts/delivery/README.md).
Both passed Windows/Ubuntu actual11/10, originalstress and installed Release
consumers. Qualification does not imply rebuilt-binary equality or turn these
diagnostic ratios into universal performance claims. The earlier Windows10 fixture
failure and correction remain linked in the older receipt.

## Improvements within Lython

These are separate source-pair observations; they cannot be added or substituted
for the CPython ratios above.

| Change | Established outcome | Limits / evidence |
| --- | --- | --- |
| Buffered yield transport | Complete text normalization warm elapsed8–11% lower/fresh8–11% lower, supporting CPU; allocation/peaks flat | Separate1bce52ad versusabab8760 pair. Positional-call CPU remains uncertain/possibly adverse. [Evidence](../benchmarks/results/2026-11-18-buffered-yield-transport/README.md). Full delivery pending. |
| Direct global operand transport | Floating warm elapsed7–9% lower with supporting CPU; allocation/peaks flat | Separateabab8760 versus65fcf7d0 pair. Fresh CPU/text/call controls uncertain; original adverse source retained. [Evidence](../benchmarks/results/2026-11-18-direct-global-operands/refined/README.md); [full qualification](../benchmarks/results/2026-11-18-direct-global-operands/refined/delivery/README.md). |
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
