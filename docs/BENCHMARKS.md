# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration edit | 181 B / 16 paths | 0.0765–0.0773 ms | 0.0616–0.0623 ms | 1.23–1.25× | Diagnostic bdab/7170. Controls L/C 1.08 / 1.09%. IQR ≤0.58%. |
| Configuration manifest | 833 B / 4 declared targets | 0.189–0.192 ms | 0.154–0.156 ms | 1.21–1.24× | Diagnostic bdab/7170. Controls L/C 1.42 / 1.22%. IQR ≤1.46%. |
| Formatted report | 3,072 records / 189,441 B | 2.934–2.942 ms | 2.671–2.737 ms | 1.07–1.10× | Diagnostic627/5ae; controls L/C 0.27/2.47%; IQR≤5.34% |
| JSON-lines record transform | 4,096 records / 110,592 B | 3.898–4.012 ms | 4.388–4.423 ms | 0.88–0.91× | Diagnostic ea8/cf; controls L/C 2.93/0.79%; IQR≤0.79% |
| Numeric CSV quality report | 4,096 records / 127,031 B | 4.066–4.166 ms | 4.574–4.693 ms | 0.87–0.91× | Diagnostic; controls L/C 2.46/2.60%; IQR≤1.47% |
| Text normalization | 52,224 B / 4,096 lines | 0.774–0.785 ms | 0.725–0.737 ms | 1.05–1.08× | Diagnostic bdab/7170. Controls L/C 1.38 / 1.53%. IQR ≤0.99%. |
| JSON-lines aggregation with a helper | 4,096 records / 110,592 B | 3.444–3.606 ms | 4.227–4.238 ms | 0.81–0.85× | Diagnostic R534/40db; Lython control 4.69%; IQR≤2.36% |
| Floating JSON numeric report | 4,096 pairs / 56,495 B | 1.036–1.063 ms | 0.833–0.835 ms | 1.24–1.28× | Diagnostic R534/40db; controls L/C 2.56/0.33%; IQR≤1.64% |
| Helper text normalization | 4,096 lines / 52,224 B | 1.130–1.137 ms | 0.856–0.907 ms | 1.25–1.33× | Diagnostic3f/a503; controls L/C 0.57/6.02%; IQR≤1.52% |
| CSV field validation | 4,128 rows / 128,183 B | 2.975–3.003 ms | 4.311–4.327 ms | 0.69–0.70× | Diagnostic7db/c036; controls L/C 0.92/0.37%; IQR≤2.99% |

**Ratio = Lython elapsed / CPython elapsed; above one means slower.** Ranges cover
the combinations of two process medians per engine, not confidence intervals.
Controls L/C report the difference between the two Lython/CPython medians. These
complete scripts show substantial text/numeric gaps alongside faster CSV/JSON-lines.

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

The configuration edit and manifest rows and the warm text control use
**bdab88c54 / 7170869d**, measured **2026-11-18** against stock **CPython 3.13.16**
on the same quiet VM and toolchains. The text control's CPython time moved from
0.501–0.503 ms in the earlier collection to 0.725–0.737 ms here with the same
source and fixture. Cross-collection movement remains unexplained.
[New observations](../benchmarks/results/2026-11-18-json-options-cpython/README.md) ·
[Earlier configuration](../benchmarks/results/2026-11-18-agent-cpython-checkpoint/README.md) ·
[Earlier text phases](../benchmarks/results/2026-11-18-affected-cpython-checkpoint/README.md).

Text normalization compilation and compile-to-result still describe
**2e585229 / 1bce52ad**. The new checkpoint refreshed only warm execution.

Helper text rows use committed **3f873227 / a5038a28**,
measured **2026-11-18** on the same machine/toolchains. [Full44256409242](../benchmarks/results/2026-11-18-prepared-module-bindings/delivery/README.md) passed Windows/Ubuntu actual11/10, original stress and installed Release consumers.
Helper warm CPython control movement of 6.02% and compile-to-result movement of
3.48% limit precision. That checkpoint also preserves the previous floating
report measurements. [Exact source and raw data](../benchmarks/results/2026-11-18-bindings-cpython-checkpoint/README.md).

The floating report and warm JSON-lines aggregation were measured **2026-11-18**
using frozen **40db9318**, with the numeric JSON change on **dd4c5165c**, and stock
**CPython 3.13.16** on the same VM and toolchains. The JSON change is retained in
**55fe3da03**. The measured image predates the merged regex change. The integer
JSON fixture has 1,024 blank lines in addition to its data records. Lython's
4.69% control spread limits the precision of that row.
[Source, timing boundaries and observations](../benchmarks/results/2026-11-18-json-numeric-cpython/README.md).

CSV validation retains **7db1de02 / c0367d98**; its original full run failed entry-
error-context regressions. Later corrected source qualified independently; these
earlier timings are not relabelled. [Sources and limitation](../benchmarks/results/2026-11-18-helper-validation-cpython/README.md).

Warm clocks include Run, mediated RAM-backed file I/O, stdout/file golden checks
and previous-result cleanup. Lython's host supplies efficient range/append I/O;
CPython uses its native file APIs without Lython's containment/ownership checks.
Fixture reset, request framing, process startup and final cache teardown are outside
guest clocks. Wider process CPU includes reset/protocol and is preserved separately.
This is useful workload latency, not equivalent containment or cold startup.

## Compilation and compile-to-result

| Workload | Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration edit | Compilation only | 0.0362–0.0368 ms | 0.0341–0.0344 ms | 1.05–1.08× | Diagnostic bdab/7170. Controls L/C 1.56 / 0.97%. IQR ≤1.20%. |
| Configuration edit | Compile-to-result | 0.135–0.136 ms | 0.124–0.124 ms | 1.09–1.10× | Diagnostic bdab/7170. Controls L/C 0.34 / 0.17%. IQR ≤1.33%. |
| Configuration manifest | Compilation only | 0.198–0.198 ms | 0.157–0.157 ms | 1.26–1.26× | Diagnostic bdab/7170. Controls L/C 0.36 / 0.00%. IQR ≤1.16%. |
| Configuration manifest | Compile-to-result | 0.489–0.496 ms | 0.462–0.473 ms | 1.03–1.07× | Diagnostic bdab/7170. Controls L/C 1.41 / 2.39%. IQR ≤1.65%. |
| JSON-lines record transform | Compilation only | 0.057–0.058 ms | 0.056–0.056 ms | 1.01–1.02× | Diagnostic; controls L/C 1.68/0.08%; IQR≤1.81% |
| JSON-lines record transform | Compile-to-result | 4.085–4.220 ms | 4.517–4.546 ms | 0.90–0.93× | Diagnostic; controls L/C 3.31/0.63%; IQR≤1.11% |
| Numeric CSV quality report | Compilation only | 0.056–0.056 ms | 0.044–0.045 ms | 1.26–1.26× | Diagnostic; controls L/C 0.41/0.23%; IQR≤1.26% |
| Numeric CSV quality report | Compile and execute once | 4.220–4.315 ms | 4.613–4.681 ms | 0.90–0.94× | Diagnostic; controls L/C 2.25/1.47%; IQR≤0.79% |
| Text normalization | Compilation only | 0.036–0.036 ms | 0.037–0.037 ms | 0.95–0.96× | Diagnostic2e5/1bce; controls L/C 0.12/0.78%; IQR≤0.66% |
| Text normalization | Compile-to-result | 0.873–0.878 ms | 0.593–0.595 ms | 1.47–1.48× | Diagnostic2e5/1bce; controls L/C 0.56/0.39%; IQR≤0.35% |
| JSON-lines aggregation with a helper | Compilation only | 0.054–0.054 ms | 0.054–0.054 ms | 0.99–1.00× | Diagnostic; controls L/C 0.63/0.11%; IQR≤0.91% |
| JSON-lines aggregation with a helper | Compile and execute once | 3.642–3.707 ms | 4.192–4.194 ms | 0.87–0.88× | Diagnostic; controls L/C 1.78/0.05%; IQR≤0.45% |
| Floating JSON numeric report | Compilation only | 0.0557–0.0560 ms | 0.0604–0.0606 ms | 0.92–0.93× | Diagnostic R534/40db; controls L/C 0.38/0.17%; IQR≤1.24% |
| Floating JSON numeric report | Compile-to-result | 1.138–1.139 ms | 0.964–0.972 ms | 1.17–1.18× | Diagnostic R534/40db; controls L/C 0.14/0.86%; IQR≤1.26% |
| Helper text normalization | Compilation only | 0.047–0.047 ms | 0.044–0.044 ms | 1.05–1.06× | Diagnostic3f/a503; controls L/C 0.47/0.39%; IQR≤0.97% |
| Helper text normalization | Compile-to-result | 1.229–1.231 ms | 0.802–0.830 ms | 1.48–1.53× | Diagnostic3f/a503; controls L/C 0.13/3.48%; IQR≤3.43% |
| CSV field validation | Compilation only | 0.026–0.026 ms | 0.027–0.027 ms | 0.96–0.98× | Diagnostic7db/c036; controls L/C 1.70/0.30%; IQR≤2.30% |
| CSV field validation | Compile-to-result | 2.976–3.039 ms | 4.384–4.402 ms | 0.68–0.69× | Diagnostic7db/c036; controls L/C 2.12/0.42%; IQR≤0.74% |

Compilation includes compiled-owner disposal. Compile-to-result includes compilation,
one execution and compiled-owner disposal. Separately warmed medians are not
additive. Warm-only numbers do not describe single-use scripts; use compile-to-result.
Helper warm CPython's6.02%control movement limits precision; all medians remain
visible. Process startup was not measured.

## Uncertainty and earlier measurements

The helper warm/fresh controls, floating warm control, report IQR and distributions stay
visible above. The [previous guide](../benchmarks/results/2026-11-17-current-cpython-checkpoint/prior-BENCHMARKS.md)
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
