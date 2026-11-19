# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration edit | 181 B / 16 paths | 0.0766–0.0779 ms | 0.06142–0.06219 ms | 1.23–1.27× | B1002 diagnostic. Controls L/C 1.70 / 1.25%. IQR ≤1.83%. |
| Configuration manifest | 833 B / 4 declared targets | 0.189–0.192 ms | 0.154–0.156 ms | 1.21–1.24× | Diagnostic bdab/7170. Controls L/C 1.42 / 1.22%. IQR ≤1.46%. |
| Formatted report | 3,072 records / 189,441 B | 2.934–2.942 ms | 2.671–2.737 ms | 1.07–1.10× | Diagnostic627/5ae; controls L/C 0.27/2.47%; IQR≤5.34% |
| Shipment report with format selectors | 3,072 records / 189,441 B | 4.347–4.428 ms | 3.060–3.069 ms | 1.42–1.45× | B1030 diagnostic. Controls L/C 1.88 / 0.31%. IQR ≤1.70%. |
| Expanded forwarding report | 1,024 records / 26,112 B | 2.150–2.172 ms | 1.182–1.185 ms | 1.81–1.84× | B1030 diagnostic. Controls L/C 0.98 / 0.23%. IQR ≤0.52%. |
| Unannotated rule closures | 2,048 inputs / 4,096 definitions | 2.892–3.047 ms | 0.682–0.684 ms | 4.23–4.47× | B1030 diagnostic. Controls L/C 5.37 / 0.20%. IQR ≤1.10%. |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 2.157–2.246 ms | 0.405–0.411 ms | 5.25–5.54× | B1030 diagnostic. Controls L/C 4.09 / 1.38%. IQR ≤3.95%. |
| JSON-lines record transform | 4,096 records / 110,592 B | 3.898–4.012 ms | 4.388–4.423 ms | 0.88–0.91× | Diagnostic ea8/cf; controls L/C 2.93/0.79%; IQR≤0.79% |
| Numeric CSV quality report | 4,096 records / 127,031 B | 4.066–4.166 ms | 4.574–4.693 ms | 0.87–0.91× | Diagnostic; controls L/C 2.46/2.60%; IQR≤1.47% |
| CSV invoice | 4,096 data records / 127,031 B | 6.676–6.825 ms | 6.042–6.049 ms | 1.10–1.13× | B1030 diagnostic. Controls L/C 2.23 / 0.11%. IQR ≤0.53%. |
| Text normalization | 52,224 B / 4,096 lines | 0.377–0.382 ms | 0.676–0.679 ms | 0.56–0.57× | Diagnostic 4ff69/74d1. Controls L/C 1.36 / 0.53%. CPython varies across collections. |
| JSON-lines aggregation with a helper | 4,096 records / 110,592 B | 3.444–3.606 ms | 4.227–4.238 ms | 0.81–0.85× | Diagnostic R534/40db; Lython control 4.69%; IQR≤2.36% |
| Floating JSON numeric report | 4,096 pairs / 56,495 B | 1.036–1.063 ms | 0.833–0.835 ms | 1.24–1.28× | Diagnostic R534/40db; controls L/C 2.56/0.33%; IQR≤1.64% |
| Helper text normalization | 52,224 B / 4,096 lines | 1.025–1.026 ms | 0.728–0.732 ms | 1.40–1.41× | B1006 diagnostic. Controls L/C 0.06 / 0.66%. IQR ≤10.08%. |
| CSV field validation | 4,128 data records / 128,183 B | 2.821–2.860 ms | 4.313–4.348 ms | 0.65–0.66× | Diagnostic c14c/1264. Controls L/C 1.41 / 0.82%. IQR ≤0.59%. |

**Ratio = Lython elapsed / CPython elapsed. Above one means slower.** Ranges cover
the combinations of two process medians per engine, not confidence intervals.
Controls L/C report the difference between the two Lython/CPython medians.
Rule closures have the largest execution gaps, followed by expanded forwarding.
Lython compiles all five B1030 scripts faster than CPython. Direct normalization,
CSV validation and the JSON-lines scripts run faster in their recorded checkpoints.

B1030 measured **2409f0596 / frozen 7e525683** on **2026-11-19 UTC** against
stock **CPython 3.13.16** on the quiet **Azure F4as_v7**, with Ubuntu 24.04.4
and .NET 11 RC3. Each process ran all three phases for each case. The two rule
scripts have 4.09–5.37% same-source Lython warm spread, which limits comparisons
with earlier runs. The measured runtime later passed the full delivery matrix.
[B1030 observations](../benchmarks/results/2026-11-19-shared-metadata-cpython/README.md) · [Earlier B1022 snapshot](../benchmarks/results/2026-11-19-function-and-split-cpython/README.md) · [Delivery](../benchmarks/results/2026-11-19-function-record-keys/delivery/README.md).

B1006 measured **03bb07cf5 / frozen d97f6f98** on **2026-11-18 UTC** against
stock **CPython 3.13.16** on the quiet **Azure F4as_v7**, with Ubuntu 24.04.4 and
.NET 11 RC3. It alternated all three phases in the same processes for rule
closures, expanded forwarding and helper text. Configuration edit uses B1002
at **53df08565 / 6c4cb50a** on the same VM and toolchains.
[B1006 source and observations](../benchmarks/results/2026-11-18-post-transport-cpython-checkpoint/README.md) ·
[B1002 source and observations](../benchmarks/results/2026-11-18-direct-defaults-cpython-checkpoint/README.md).

The numeric CSV and earlier JSON-lines compilation and compile-to-result rows were measured **2026-11-17 UTC**, committed
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

The formatted-report row uses qualified **62786960 / 5ae019db**,
measured2026-11-18 on the same machine/toolchains; report CPython IQR5.34%limits
precision. [Sources and controls](../benchmarks/results/2026-11-18-agent-cpython-checkpoint/README.md).

The configuration manifest and earlier edit results use
**bdab88c54 / 7170869d**, measured **2026-11-18** against stock **CPython 3.13.16**
on the same quiet VM and toolchains.
[New observations](../benchmarks/results/2026-11-18-json-options-cpython/README.md) ·
[Earlier configuration](../benchmarks/results/2026-11-18-agent-cpython-checkpoint/README.md) ·
[Earlier text phases](../benchmarks/results/2026-11-18-affected-cpython-checkpoint/README.md).

Warm direct text normalization and earlier helper phase results use **4ff69a9af / 74d19a10**,
measured **2026-11-18** against stock **CPython 3.13.16** on the same quiet VM
and .NET 11 RC3. The remaining direct normalization phases use the preceding
**6f494109f / e4905aa5** image. CPython warm normalization has ranged from
0.501–0.503 ms to 0.733–0.737 ms across collections, with no established cause.
In the earlier independently warmed comparison, CPython helper
compile-to-result measured lower than warm. The inversion did not recur in
same-process runs, including B1006. The helper remained
slower in Lython in both phases.
Use paired Lython comparisons to attribute code changes.
[Current text checkpoint](../benchmarks/results/2026-11-18-line-count-cpython-checkpoint/README.md) ·
[Same-process diagnostic](../benchmarks/results/2026-11-18-current-helper-phases/README.md) ·
[Preceding text checkpoint](../benchmarks/results/2026-11-18-normalization-cpython-checkpoint/README.md) ·
[Earlier helper measurements](../benchmarks/results/2026-11-18-bindings-cpython-checkpoint/README.md).

The floating report and warm JSON-lines aggregation were measured **2026-11-18**
using frozen **40db9318**, with the numeric JSON change on **dd4c5165c**, and stock
**CPython 3.13.16** on the same VM and toolchains. The JSON change is retained in
**55fe3da03**. The measured image predates the merged regex change. The integer
JSON fixture has 1,024 blank lines in addition to its data records. Lython's
4.69% control spread limits the precision of that row.
[Source, timing boundaries and observations](../benchmarks/results/2026-11-18-json-numeric-cpython/README.md).

The CSV field validation and earlier invoice results use **c14c7c92b / 12643cec**,
measured **2026-11-18** against stock **CPython 3.13.16** on the same quiet VM.
Its text control and earlier text observations remain in the evidence.
[CSV checkpoint](../benchmarks/results/2026-11-18-csv-cpython-checkpoint/README.md) · [Earlier validation source and failed-delivery limitation](../benchmarks/results/2026-11-18-helper-validation-cpython/README.md).

Warm clocks include Run, mediated RAM-backed file I/O, stdout/file golden checks
and previous-result cleanup. Lython's host supplies efficient range/append I/O;
CPython uses its native file APIs without Lython's containment/ownership checks.
Fixture reset, request framing, process startup and final cache teardown are outside
guest clocks. Wider process CPU includes reset/protocol and is preserved separately.
This is useful workload latency, not equivalent containment or cold startup.

## Compilation and compile-to-result

| Workload | Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration edit | Compilation only | 0.03577–0.03642 ms | 0.03409–0.03420 ms | 1.05–1.07× | B1002 diagnostic. Controls L/C 1.82 / 0.33%. IQR ≤1.21%. |
| Configuration edit | Compile-to-result | 0.1336–0.1345 ms | 0.1235–0.1260 ms | 1.06–1.09× | B1002 diagnostic. Controls L/C 0.66 / 1.98%. IQR ≤0.60%. |
| Expanded forwarding report | Compilation only | 0.05108–0.05153 ms | 0.06128–0.06156 ms | 0.83–0.84× | B1030 diagnostic. Controls L/C 0.88 / 0.46%. IQR ≤0.69%. |
| Expanded forwarding report | Compile-to-result | 2.255–2.285 ms | 1.322–1.330 ms | 1.70–1.73× | B1030 diagnostic. Controls L/C 1.32 / 0.62%. IQR ≤0.25%. |
| Unannotated rule closures | Compilation only | 0.03312–0.03357 ms | 0.04176–0.04191 ms | 0.79–0.80× | B1030 diagnostic. Controls L/C 1.35 / 0.35%. IQR ≤0.67%. |
| Unannotated rule closures | Compile-to-result | 2.976–3.122 ms | 0.725–0.731 ms | 4.07–4.31× | B1030 diagnostic. Controls L/C 4.89 / 0.79%. IQR ≤0.60%. |
| Annotated rule closures | Compilation only | 0.02525–0.02529 ms | 0.03111–0.03115 ms | 0.81× | B1030 diagnostic. Controls L/C 0.18 / 0.12%. IQR ≤0.94%. |
| Annotated rule closures | Compile-to-result | 2.187–2.307 ms | 0.435–0.443 ms | 4.94–5.31× | B1030 diagnostic. Controls L/C 5.53 / 1.77%. IQR ≤1.43%. |
| Shipment report with format selectors | Compilation only | 0.03101–0.03112 ms | 0.03199–0.03202 ms | 0.97× | B1030 diagnostic. Controls L/C 0.34 / 0.09%. IQR ≤0.56%. |
| Shipment report with format selectors | Compile-to-result | 4.401–4.497 ms | 3.171–3.222 ms | 1.37–1.42× | B1030 diagnostic. Controls L/C 2.20 / 1.60%. IQR ≤2.25%. |
| Configuration manifest | Compilation only | 0.198–0.198 ms | 0.157–0.157 ms | 1.26–1.26× | Diagnostic bdab/7170. Controls L/C 0.36 / 0.00%. IQR ≤1.16%. |
| Configuration manifest | Compile-to-result | 0.489–0.496 ms | 0.462–0.473 ms | 1.03–1.07× | Diagnostic bdab/7170. Controls L/C 1.41 / 2.39%. IQR ≤1.65%. |
| JSON-lines record transform | Compilation only | 0.057–0.058 ms | 0.056–0.056 ms | 1.01–1.02× | Diagnostic; controls L/C 1.68/0.08%; IQR≤1.81% |
| JSON-lines record transform | Compile-to-result | 4.085–4.220 ms | 4.517–4.546 ms | 0.90–0.93× | Diagnostic; controls L/C 3.31/0.63%; IQR≤1.11% |
| Numeric CSV quality report | Compilation only | 0.056–0.056 ms | 0.044–0.045 ms | 1.26–1.26× | Diagnostic; controls L/C 0.41/0.23%; IQR≤1.26% |
| Numeric CSV quality report | Compile and execute once | 4.220–4.315 ms | 4.613–4.681 ms | 0.90–0.94× | Diagnostic; controls L/C 2.25/1.47%; IQR≤0.79% |
| Text normalization | Compilation only | 0.03568–0.03616 ms | 0.03765–0.03796 ms | 0.94–0.96× | Diagnostic 6f494/e490. Controls L/C 1.36 / 0.81%. |
| Text normalization | Compile-to-result | 0.510–0.515 ms | 0.845–0.850 ms | 0.60–0.61× | Diagnostic 6f494/e490. Controls L/C 0.95 / 0.53%. |
| JSON-lines aggregation with a helper | Compilation only | 0.054–0.054 ms | 0.054–0.054 ms | 0.99–1.00× | Diagnostic; controls L/C 0.63/0.11%; IQR≤0.91% |
| JSON-lines aggregation with a helper | Compile and execute once | 3.642–3.707 ms | 4.192–4.194 ms | 0.87–0.88× | Diagnostic; controls L/C 1.78/0.05%; IQR≤0.45% |
| Floating JSON numeric report | Compilation only | 0.0557–0.0560 ms | 0.0604–0.0606 ms | 0.92–0.93× | Diagnostic R534/40db; controls L/C 0.38/0.17%; IQR≤1.24% |
| Floating JSON numeric report | Compile-to-result | 1.138–1.139 ms | 0.964–0.972 ms | 1.17–1.18× | Diagnostic R534/40db; controls L/C 0.14/0.86%; IQR≤1.26% |
| Helper text normalization | Compilation only | 0.04545–0.04546 ms | 0.04427–0.04435 ms | 1.02–1.03× | B1006 diagnostic. Controls L/C 0.04 / 0.18%. IQR ≤1.01%. |
| Helper text normalization | Compile-to-result | 1.105–1.107 ms | 0.835–0.857 ms | 1.29–1.33× | B1006 diagnostic. Controls L/C 0.21 / 2.63%. IQR ≤2.13%. |
| CSV invoice | Compilation only | 0.09022–0.09083 ms | 0.09345–0.09416 ms | 0.96–0.97× | B1030 diagnostic. Controls L/C 0.68 / 0.76%. IQR ≤0.92%. |
| CSV invoice | Compile-to-result | 6.867–7.022 ms | 6.256–6.298 ms | 1.09–1.12× | B1030 diagnostic. Controls L/C 2.27 / 0.67%. IQR ≤0.70%. |
| CSV field validation | Compilation only | 0.0250–0.0251 ms | 0.0265–0.0267 ms | 0.93–0.94× | Diagnostic c14c/1264. Controls L/C 0.29 / 0.72%. IQR ≤0.66%. |
| CSV field validation | Compile-to-result | 3.146–3.147 ms | 4.663–4.669 ms | 0.67× | Diagnostic c14c/1264. Controls L/C 0.05 / 0.12%. IQR ≤1.11%. |

Compilation includes compiled-owner disposal. Compile-to-result includes compilation,
one execution and compiled-owner disposal. Separately warmed medians are not
additive. Warm-only numbers do not describe single-use scripts; use compile-to-result.
Earlier cold-start results have their own measurement boundaries.

## Uncertainty and earlier measurements

Helper-text timing remains less precise than the other B1006 workloads. Its warm IQR
reaches 10.08% and compile-to-result spreads reach 2.63%. The earlier B993
configuration, invoice, helper and forwarding results are historical.
[B993 timings and controls](../benchmarks/results/2026-11-18-retained-cpython-checkpoint/README.md). The [previous guide](../benchmarks/results/2026-11-17-current-cpython-checkpoint/prior-BENCHMARKS.md)
preserves earlier helper-text/report, configuration, file-search and CSV invoice
measurements with their named revisions, original adverse controls and unresolved
phase inversions. Those results are historical, not this checkpoint or today's HEAD.
The new ratios do not establish a source-change gain across collections.

The older0d and627production inputs passed Windows/Ubuntu actual11/10, original
stress and installed Release consumers; the new2e5production inputs are also
[fully qualified](../benchmarks/results/2026-11-18-buffered-yield-transport/delivery/README.md).
Qualification does not imply rebuilt-binary equality or universal performance.
Original adverse controls and failed conditions remain linked in the evidence.
[New3f/a503 full qualification](../benchmarks/results/2026-11-18-prepared-module-bindings/delivery/README.md) passed independently; diagnostic ratios are
specific to that frozen source/image.

## Improvements within Lython

These are separate source-pair observations; they cannot be added or substituted
for the CPython ratios above.

| Change | Established outcome | Limits / evidence |
| --- | --- | --- |
| Exact positional method formatting | Expanded forwarding improves 4.11–6.31% warm with lower CPU and 6.62% lower local allocation. The positional-format variant improves 3.34–5.40% warm and 2.88–4.59% compile-to-result, with lower CPU and 8.09% lower allocation. | Both accounted peaks fall 52,736 bytes. Expanded compile-to-result improves 0.85–4.39% on confirmation with mixed CPU. The initial named-format slowdown did not recur. [B1038/B1039](../benchmarks/results/2026-11-19-final-method-format/README.md). |
| Direct definition records | Rule allocation falls 3.07% and 4.19% at the same accounted peaks. Annotated compile-to-result improves 1.98–6.24% with lower CPU. Confirmation shows 3.43–5.94% lower annotated warm elapsed with lower CPU. | First-run annotated warm is inconclusive. Forwarding’s initial elapsed increase did not recur. Reused-default timing remains unresolved. [B1031/B1032](../benchmarks/results/2026-11-19-direct-definition-records/README.md). |
| Shared function metadata keys | Allocations fall 9.06% for unannotated rules and 15.85% for annotated rules. Accounted peaks fall 21.45% and 29.43%. Both annotated compile-to-result runs use less CPU. | Single-definition allocation rises 1.68%. Annotated compilation-only time rises 0.55–1.90%. The unannotated compile-to-result slowdown did not recur, and reused-default timing remains unresolved. [B1028/B1029](../benchmarks/results/2026-11-19-function-record-keys/README.md). |
| Prepared method-format selectors | Shipment report with format subscripts improves 4.13–6.65% warm and 1.12–2.95% compile-to-result with lower CPU. Allocation falls 6.14%. | Accounted peak rises 384 bytes. The ordinary method-format and f-string elapsed controls span zero. [B1025](../benchmarks/results/2026-11-19-prepared-format-selectors/README.md). |
| Split fields into final unpack operands | Expanded forwarding improves 1.72–3.02% warm and 1.52–3.58% compile-to-result, with lower CPU. Local allocation falls 4.26% and the accounted peak falls 11.34%. | The positional control spans zero. The text control reference elapsed spread is 2.22%. [B1021](../benchmarks/results/2026-11-19-final-split-operands/README.md). |
| Direct function-definition completion | Annotated rules improve 1.85–2.48% warm and 3.34–5.93% compile-to-result, with lower process CPU. | Unannotated elapsed and the positional control span zero. Local allocations and accounted peaks agree. [B1020](../benchmarks/results/2026-11-19-direct-function-completion/README.md). |
| Direct postfix syntax storage | Compilation improves 2.11–3.95% for the manifest and 2.33–2.97% for the generated validator, with lower process CPU. The validator allocates 7.40% less in a local first-compile diagnostic. | Warm elapsed controls and manifest compile-to-result CPU span zero. [B1017](../benchmarks/results/2026-11-19-postfix-syntax-storage/README.md). |
| Shared function annotation inputs | Annotated rule closures improve 7.11–9.90% warm and 6.56–8.06% compile-to-result with supporting CPU. Allocation falls 11.99% at the same accounted peak. | Compilation, unannotated rules and positional warm timing span zero. [B1007](../benchmarks/results/2026-11-18-function-annotation-inputs/README.md). |
| Direct trailing keyword dictionaries | File forwarding improves 16.36–17.89% warm and 16.33–16.96% compile-to-result with supporting CPU. Allocation falls 20.73% at the same accounted peak. | The mutable bound-method report improves 24.44–26.92% warm. Positional timing spans zero and compile CPU is mixed. [B1005](../benchmarks/results/2026-11-18-exact-keyword-transport/README.md). |
| Executable function definition inputs | Rule closures improve 14.65–16.84% warm and 14.24–15.08% compile-to-result with supporting CPU. Allocation falls 9.52% at the same accounted peak. | Compilation alone rises 2.60–4.54%. Reused-default and positional warm controls span zero. [B1004](../benchmarks/results/2026-11-18-executable-definition-inputs/README.md). |
| Direct function annotation construction | Annotated rule closures improve 28.32–31.00% warm and 27.52–31.55% compile-to-result with supporting CPU. Allocation falls 16.95%. | Compilation alone rises 3.68–4.21%. The positional warm control rises 0.40–1.42% with supporting CPU. Unannotated control CPU spans zero. [B1009](../benchmarks/results/2026-11-18-function-annotation-storage/README.md). |
| Fixed function metadata | Annotated rules improve 0.76–3.62% warm. Unannotated rules improve 1.18–3.17% compile-to-result. CPU supports both. Local allocation falls 9.21% and 2.72%, respectively. | The unannotated accounted peak rises 2.59%. Unannotated warm, compilation alone and positional elapsed remain inconclusive. [B1010](../benchmarks/results/2026-11-18-function-metadata-slots/README.md). |
| Direct trailing keyword operands | Forwarding improves 5.61–6.27% warm and 2.80–5.64% compile-to-result, with CPU support. Allocation falls 6.77% at the same accounted peak. | Mutable bound calls allocate 14.01% less and use less CPU. Their elapsed IQR reaches 7.83%. The positional control spans zero. [B1012](../benchmarks/results/2026-11-18-trailing-keyword-operands/README.md). |
| Precomputed abstract-value effects | Manifest compilation improves 5.55–8.99% and generated validator compilation 1.62–3.03%, with CPU support. | Manifest compile-to-result elapsed improves 1.14–3.40%, with CPU spanning zero. Both warm controls span zero. [B1015](../benchmarks/results/2026-11-18-abstract-value-facts/README.md). |
| Direct function default construction | Rule-closure report warm elapsed 22.84–25.49% lower, compile-to-result 21.47–22.52% lower and allocation 32.66% lower. | Workload creates both functions and lambdas per rule. Reused-default calls were 0.12–1.65% slower with supporting process CPU. [B1001](../benchmarks/results/2026-11-18-direct-function-defaults/README.md). |
| Borrowed `str.format` arguments | Positional method-format report warm elapsed 4.68–8.68% lower with supporting CPU and 13.35% lower allocation. Expanded forwarding compile-to-result is 1.18–2.64% lower. | Named formatting spans zero. The positional-call control was adverse in B999 and spanned zero in B1000. The formatting gain repeated. [B999](../benchmarks/results/2026-11-18-borrowed-format-packet/README.md) · [B1000](../benchmarks/results/2026-11-18-borrowed-format-packet/confirmation/README.md). |
| UTF-8 guest keyword binding | Expanded forwarding report compile-to-result elapsed 5.37–6.28% lower with supporting process CPU. Report allocation 8.15% lower and accounted peak 4.48% lower. | Warm direction is favorable with 5.05% candidate spread. Ordinary named warm and f-string report controls span zero. The earlier prototype's adverse observations remain recorded. [B998](../benchmarks/results/2026-11-18-compact-keyword-layout/README.md). |
| Deferred duplicate index for exact keyword dictionaries | Expanded forwarding report warm elapsed 3.71–5.12% lower, process CPU 4.32–6.71% lower and local allocation 6.84% lower at the same accounted peak. | Separate quiet B996 source pair. Positional elapsed and CPU span zero. [Comparison](../benchmarks/results/2026-11-18-lazy-keyword-index/README.md). |
| Direct fixed-list unpack | Expanded forwarding report warm elapsed 2.85–3.85% lower with supporting CPU and 5.05% lower local allocation. | Separate quiet B994 source pair. Positional elapsed spans zero. [Comparison](../benchmarks/results/2026-11-18-fixed-list-unpack/README.md). |
| Bulk exact-list drains | Helper normalization warm elapsed 2.89–7.82% lower with supporting process CPU. Allocation and accounted peaks were effectively flat. | 45385d6c versus 610d403d. The warm positional control also moved favorably and the helper reference spread was 4.31%. Helper report and CSV remain inconclusive. [Comparison](../benchmarks/results/2026-11-18-exact-list-drain/README.md). |
| Private split-line counts | Normalization warm elapsed 15.01–15.56% lower. Helper text elapsed 6.27–8.03% lower warm and 5.45–7.27% lower compile-to-result. Process CPU supports both. | 74d19a10 versus e4905aa5. Local allocation 27.47% and 15.91% lower, respectively. Validated CSV movement remains inconclusive. [Comparison](../benchmarks/results/2026-11-18-private-line-count/README.md). |
| Direct trimmed split joins | Normalization elapsed 42.38–43.42% lower warm and 39.52–40.36% lower compile-to-result, with supporting process CPU. Local allocation and accounted peak fell 40.59% and 47.79%. | e4905aa5 versus 93739eed. The change removes the private split list, slices, generator and drain for the exact builtin call sequence. Helper text and validated CSV elapsed controls span zero. [Comparison](../benchmarks/results/2026-11-18-trimmed-split-join/README.md) · [Release qualification](../benchmarks/results/2026-11-18-trimmed-split-join/delivery/README.md). |
| CSV dictionary reader line numbers | Validated records allocation 16.53% lower, warm elapsed 3.63–6.34% lower and compile-to-result elapsed 5.07–5.50% lower, with supporting CPU | 12643cec versus 4eb89711. Invoice allocation 9.37% lower. Invoice timing overlaps positional control movement. Accounted peaks match. [Comparison](../benchmarks/results/2026-11-18-csv-line-number/README.md). |
| Governed JSON encoder frames | Scaled manifest allocation 4.15% lower and compile-to-result elapsed 1.00–1.36% lower with supporting CPU | 33025cee versus 7170869d. Small manifest allocation 1.96% lower. Accounted peaks rose 608 bytes and warm timings were mixed. [Comparison](../benchmarks/results/2026-11-18-json-encoder-allocation/comparison/README.md). |
| Direct JSON output options | Config edit warm allocation 4.0% lower and elapsed 0.82–1.34% lower with supporting CPU. Manifest allocation 1.71% lower | 7170869d versus f2824fea0. Fresh elapsed mixed, accounted peaks matched and positional control elapsed rose 0.22–2.79%. [Evidence](../benchmarks/results/2026-11-18-native-json-encoding-options/README.md). |
| Admitted native text slices | Log extraction compile-to-result elapsed 2.61–4.53% lower with supporting CPU | f2824fea0 versus 27cb473e. Local allocation 1.63% higher with the same accounted peak and wider warm spread. [Comparison](../benchmarks/results/2026-11-18-string-slice-ownership/comparison/README.md) · [Full qualification](../benchmarks/results/2026-11-18-string-slice-ownership/delivery/README.md). |
| Shared JSON numeric lexing | Floating report warm 9.90–10.17% lower and initial compile-to-result 6.29–7.99% lower elapsed, with supporting CPU | 40db9318 versus 29192b95. Accounted peaks matched. Integer JSON remained mixed on confirmation. [Both comparisons](../benchmarks/results/2026-11-18-json-number-lexing/README.md). |
| Prepared module bindings | Floating report warm11–14% lower / compile-to-result7% lower elapsed, supporting CPU; warm allocation/peaks flat | Separatea5038a28 versus84b1a159 pair; compile2%slower, helper elapsed0.2–2.2%higher/CPUuncertain. [Full qualification](../benchmarks/results/2026-11-18-prepared-module-bindings/delivery/README.md) passed; original controls remain. [Initial adversity](../benchmarks/results/2026-11-18-prepared-module-bindings/initial/README.md), [confirmation](../benchmarks/results/2026-11-18-prepared-module-bindings/confirmation/README.md). |
| Discarded call results | CSV-validation allocation6.73% lower; named helper elapsed0.8–3.1% lower with supporting CPU | Separatec0367d98 versusa71abfed pair; positional calls0.9–2.6%slower, floating uncertain. Original c036 full delivery failed entry-error context; [corrected9e/84](../benchmarks/results/2026-11-18-result-entry-correction/delivery/README.md) is fully qualified. Separate correction timing adds1–2%helper elapsed debt; do not combine collections. [Evidence](../benchmarks/results/2026-11-18-discarded-call-results/refined/README.md). |
| Known-size working drains | Helper-list text allocation7.11% lower; original/nontext effectively flat | Separatea71abfed versus2ede09fc pair; elapsed/CPU gains inconclusive, adverse first collection retained. [Containing corrected9e/84 delivery](../benchmarks/results/2026-11-18-result-entry-correction/delivery/README.md) fully passed; these earlier timings remain separate. [Evidence](../benchmarks/results/2026-11-18-known-drain-construction/confirmation/README.md). |
| Direct split construction | Complete text allocation17.319% lower; warm elapsed1.8–2.3% lower with supporting CPU | Separate2ede09fc versus1bce52ad pair. Accounted peak1.12%higher from conservative preflight; positional calls inconclusive. [Evidence](../benchmarks/results/2026-11-18-direct-split-construction/refined/README.md); [full qualification](../benchmarks/results/2026-11-18-direct-split-construction/refined/delivery/README.md). |
| Buffered yield transport | Complete text normalization warm elapsed8–11% lower/fresh8–11% lower, supporting CPU; allocation/peaks flat | Separate1bce52ad versusabab8760 pair. Positional-call CPU remains uncertain/possibly adverse. [Evidence](../benchmarks/results/2026-11-18-buffered-yield-transport/README.md); [full qualification](../benchmarks/results/2026-11-18-buffered-yield-transport/delivery/README.md). |
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
