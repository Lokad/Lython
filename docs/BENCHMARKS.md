# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Unannotated rule closures | 2,048 inputs / 4,096 definitions | 2.307–2.321 ms | 0.683–0.688 ms | 3.35–3.40× | B1122 diagnostic. Controls L/C 0.61 / 0.70%. IQR ≤0.74%. |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 1.480–1.513 ms | 0.442–0.449 ms | 3.30–3.42× | B1196 diagnostic. Controls L/C 2.22 / 1.51%. IQR ≤1.72%. |
| Nested annotated factories | 2,048 inputs / 4,096 definitions | 2.054–2.063 ms | 0.588–0.595 ms | 3.45–3.51× | B1184 diagnostic. Controls L/C 0.46 / 1.25%. IQR ≤2.87%. |
| Postponed annotated rules | 2,048 inputs / 2,048 definitions | 1.486–1.569 ms | 0.406–0.407 ms | 3.65–3.86× | B1156 diagnostic. Controls L/C 5.61 / 0.28%. IQR ≤2.34%. Gap size uncertain. |
| Postponed nested factories | 2,048 inputs / 4,096 definitions | 2.051–2.117 ms | 0.577–0.590 ms | 3.47–3.67× | B1156 diagnostic. Controls L/C 3.22 / 2.39%. IQR ≤1.50%. |
| Captured rule closures | 2,048 inputs / 4,096 returned closures | 2.437–2.454 ms | 0.776–0.790 ms | 3.09–3.16× | B1122 diagnostic. Controls L/C 0.71 / 1.69%. IQR ≤1.69%. |
| Inline forwarding with dynamic keys | 1,024 records / 26,112 B | 1.894–1.913 ms | 1.139–1.159 ms | 1.63–1.68× | B1102 diagnostic. Controls L/C 1.01 / 1.74%. IQR ≤2.35%. |
| Expanded forwarding report | 1,024 records / 26,112 B | 1.533–1.543 ms | 1.173–1.189 ms | 1.29–1.32× | B1180 diagnostic. Controls L/C 0.62 / 1.38%. IQR ≤4.03%. |
| CSV invoice | 4,096 data records / 127,031 B | 6.697–6.760 ms | 6.325–6.421 ms | 1.04–1.07× | B1196 diagnostic. Controls L/C 0.95 / 1.51%. IQR ≤1.20%. |
| Retained nonlocal siblings | 240 factories / 720 returned closures | 0.169–0.170 ms | 0.188–0.190 ms | 0.89–0.90× | B1109 diagnostic. Controls L/C 0.57 / 0.89%. IQR ≤2.72%. |
| Shipment report with named format selectors | 3,072 records / 189,441 B | 3.786–3.842 ms | 3.233–3.314 ms | 1.14–1.19× | B1190 diagnostic. Controls L/C 1.48 / 2.50%. IQR ≤3.48%. |
| Shipment report with positional format selectors | 3,072 records / 189,441 B | 3.737–3.750 ms | 2.861–2.893 ms | 1.29–1.31× | B1190 diagnostic. Controls L/C 0.35 / 1.13%. IQR ≤3.50%. |
| Generated validator | 96 guards / 1,714 B JSON | 0.081 ms | 0.043–0.044 ms | 1.83–1.87× | B1173 diagnostic. Controls L/C 0.57 / 1.49%. IQR ≤3.66%. |
| Inherited dataclass definitions | 1 base and 48 subclasses, 48 instances | 0.201–0.204 ms | 5.007–5.147 ms | 0.039–0.041× | B1196 diagnostic. Includes class construction and decoration on each run. |
| Configuration manifest | 4 targets / 833 B across 2 files | 0.199–0.200 ms | 0.165 ms | 1.21× | B1130 diagnostic. Controls L/C 0.17 / 0.06%. IQR ≤0.99%. |

**Ratio = Lython elapsed / CPython elapsed. Above one means slower.** Ranges cover
the combinations of two process medians per engine, not confidence intervals.
Controls L/C report the difference between the two Lython/CPython medians.

B1196 measures inherited dataclasses, warm eager rules and warm invoice at
**53d3b45d5 / frozen cf059b2a2** on **2026-11-20 UTC**. Both machine checks
and all 100 batches passed. B1190 measures both shipment variants and the
prior warm invoice at **6ba60528d / frozen f8f223df2** on the same date.
Both machine checks and all 140 batches passed. B1184 measures eager and
nested factories and an earlier warm invoice at **605b3cd50 /
frozen 848cbf783** on **2026-11-20 UTC**. Both machine checks and all 140
batches passed. B1180 measures forwarding and the earlier warm invoice at **c3684d4d1 / frozen
289380214** on **2026-11-20 UTC**. Both machine checks and all 80 batches
passed. B1173 measures shipment, validator and the earlier forwarding and invoice at
**c1c15c008 / frozen 8643ebfd8** on **2026-11-20 UTC**. It uses the same
source-verified core as B1172. Both machine checks and all 200 batches passed.
The original archive-floor failure and subsequent verification are recorded.
B1168 measured dataclasses, factories and warm invoice at **ab2aa9554**.
These results and the earlier B1162 report and B1136 validator results
remain in their linked records.
B1130 measures the manifest at **a472eb2a4 / frozen e18ee460**.
B1156 measures postponed factory variants at **551fde44e**, using the
exact `a6928c942` core from B1155, on **2026-11-20 UTC**.
The invoice compilation and compile-to-result rows still come from B1143,
which measured the earlier invoice and annotated rules at
**353ec074c / frozen 5e40c428**.
B1122 measures unannotated and captured rules at **0d16f8307 / frozen 99840bea**.
B1109 measures retained siblings at **3e67fd010 / frozen aa15d342**.
B1102 measures inline forwarding at **80cbf4988 / frozen f26c8272**.
Earlier rows ran **2026-11-19 UTC**. All use stock **CPython 3.13.16** on the quiet
**Azure F4as_v7** with Ubuntu 24.04.4 and .NET 11 RC3. Each collection
uses duplicate processes per engine. Each row describes its measured revision.
B1122 passed the measured scripts' output and file checks. Its source
predates the generator-lambda and iterator-cleanup fixture corrections.
[B1196 observations](../benchmarks/results/2026-11-20-cpython-executable-classes/README.md) ·
[B1190 observations](../benchmarks/results/2026-11-20-cpython-named-shipment/README.md) ·
[B1184 observations](../benchmarks/results/2026-11-20-cpython-keyword-default-factories/README.md) ·
[B1180 observations](../benchmarks/results/2026-11-20-cpython-expanded-forwarding/README.md) ·
[B1173 observations](../benchmarks/results/2026-11-20-cpython-reports-validator/README.md) ·
[B1168 observations](../benchmarks/results/2026-11-20-cpython-dataclasses-and-factories/README.md) ·
[B1162 observations](../benchmarks/results/2026-11-20-representative-cpython/README.md) ·
[B1156 observations](../benchmarks/results/2026-11-20-postponed-factories-cpython/README.md) ·
[B1151 observations](../benchmarks/results/2026-11-19-factory-cpython-checkpoint/README.md) ·
[B1143 observations](../benchmarks/results/2026-11-19-csv-rule-cpython-checkpoint/README.md) ·
[B1136 observations](../benchmarks/results/2026-11-19-native-integer-cpython/README.md) ·
[B1130 observations](../benchmarks/results/2026-11-19-postfix-cpython-checkpoint/README.md) ·
[B1122 observations](../benchmarks/results/2026-11-19-retained-lexical-cpython/README.md) ·
[B1114 observations](../benchmarks/results/2026-11-19-retained-return-cpython/README.md) ·
[B1109 history](../benchmarks/results/2026-11-19-retained-factories-cpython/README.md) ·
[B1106 observations](../benchmarks/results/2026-11-19-token-source-cpython/README.md) ·
[B1102 observations](../benchmarks/results/2026-11-19-retained-defaults-cpython/README.md) ·
[B1091 history](../benchmarks/results/2026-11-19-inline-forwarding-cpython/README.md) ·
[Earlier B1084](../benchmarks/results/2026-11-19-compiled-binding-cpython/README.md) ·
[B1069 observations](../benchmarks/results/2026-11-19-retained-trim-cpython/README.md).

Warm clocks include Run, mediated RAM-backed file I/O, stdout/file golden checks
and previous-result cleanup. Lython's host supplies efficient range/append I/O;
CPython uses its native file APIs without Lython's containment/ownership checks.
The Lython worker uses a shared memory allowance with accounting active and no
byte limit. It checks that reservations return to zero after cleanup.
Fixture reset, request framing, process startup and final cache teardown are outside
guest clocks. Wider process CPU includes reset/protocol and is preserved separately.
The table compares workload latency under different containment boundaries.

## Compilation and compile-to-result

| Workload | Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Unannotated rule closures | Compilation only | 0.03072–0.03102 ms | 0.04189–0.04201 ms | 0.73–0.74× | B1122 diagnostic. Controls L/C 1.00 / 0.30%. IQR ≤0.44%. |
| Unannotated rule closures | Compile-to-result | 2.372–2.375 ms | 0.728–0.729 ms | 3.25–3.26× | B1122 diagnostic. Controls L/C 0.13 / 0.20%. IQR ≤1.60%. |
| Annotated rule closures | Compilation only | 0.02141–0.02142 ms | 0.03213–0.03220 ms | 0.66–0.67× | B1184 diagnostic. Controls L/C 0.08 / 0.19%. IQR ≤3.97%. |
| Annotated rule closures | Compile-to-result | 1.468–1.515 ms | 0.448–0.478 ms | 3.07–3.38× | B1184 diagnostic. Controls L/C 3.14 / 6.75%. IQR ≤3.51%. Gap size uncertain. |
| Nested annotated factories | Compilation only | 0.02587–0.02632 ms | 0.03905–0.03949 ms | 0.65–0.67× | B1184 diagnostic. Controls L/C 1.74 / 1.13%. IQR ≤2.91%. |
| Nested annotated factories | Compile-to-result | 2.100–2.111 ms | 0.625–0.644 ms | 3.26–3.38× | B1184 diagnostic. Controls L/C 0.52 / 3.12%. IQR ≤2.86%. |
| Inherited dataclass definitions | Compilation only | 0.23994–0.24064 ms | 0.46485–0.46598 ms | 0.515–0.518× | B1196 diagnostic. Controls L/C 0.29 / 0.24%. IQR ≤2.75%. |
| Inherited dataclass definitions | Compile-to-result | 0.463–0.464 ms | 5.629–5.772 ms | 0.080–0.082× | B1196 diagnostic. Controls L/C 0.17 / 2.55%. IQR ≤1.24%. |
| Postponed annotated rules | Compilation only | 0.02514–0.02533 ms | 0.03518–0.03539 ms | 0.71–0.72× | B1156 diagnostic. Controls L/C 0.77 / 0.58%. IQR ≤1.42%. |
| Postponed annotated rules | Compile-to-result | 1.531–1.599 ms | 0.429–0.436 ms | 3.51–3.73× | B1156 diagnostic. Controls L/C 4.44 / 1.68%. IQR ≤2.52%. |
| Postponed nested factories | Compilation only | 0.03018–0.03026 ms | 0.04285–0.04312 ms | 0.70–0.71× | B1156 diagnostic. Controls L/C 0.27 / 0.62%. IQR ≤0.96%. |
| Postponed nested factories | Compile-to-result | 2.103–2.146 ms | 0.624–0.637 ms | 3.30–3.44× | B1156 diagnostic. Controls L/C 2.02 / 2.20%. IQR ≤2.47%. |
| Captured rule closures | Compilation only | 0.03460–0.03524 ms | 0.04742–0.04748 ms | 0.73–0.74× | B1122 diagnostic. Controls L/C 1.86 / 0.14%. IQR ≤1.31%. |
| Captured rule closures | Compile-to-result | 2.519–2.523 ms | 0.830–0.839 ms | 3.00–3.04× | B1122 diagnostic. Controls L/C 0.15 / 1.09%. IQR ≤1.51%. |
| Inline forwarding with dynamic keys | Compilation only | 0.05339–0.05370 ms | 0.06049–0.06111 ms | 0.87–0.89× | B1102 diagnostic. Controls L/C 0.57 / 1.01%. IQR ≤1.53%. |
| Inline forwarding with dynamic keys | Compile-to-result | 2.003–2.012 ms | 1.285–1.298 ms | 1.54–1.57× | B1102 diagnostic. Controls L/C 0.47 / 1.00%. IQR ≤1.66%. |
| Expanded forwarding report | Compilation only | 0.04385–0.04448 ms | 0.06131–0.06261 ms | 0.70–0.73× | B1180 diagnostic. Controls L/C 1.45 / 2.12%. IQR ≤4.25%. |
| Expanded forwarding report | Compile-to-result | 1.611–1.638 ms | 1.318 ms | 1.22–1.24× | B1180 diagnostic. Controls L/C 1.65 / 0.00%. IQR ≤3.60%. |
| CSV invoice | Compilation only | 0.07780–0.07865 ms | 0.09355–0.09493 ms | 0.82–0.84× | B1143 diagnostic. Controls L/C 1.08 / 1.48%. IQR ≤1.10%. |
| CSV invoice | Compile-to-result | 6.625–6.737 ms | 6.367–6.465 ms | 1.02–1.06× | B1143 diagnostic. Controls L/C 1.69 / 1.53%. IQR ≤0.76%. |
| Generated validator | Compilation only | 2.04870–2.05688 ms | 1.82118–1.83917 ms | 1.11–1.13× | B1173 diagnostic. Controls L/C 0.40 / 0.99%. IQR ≤3.10%. |
| Generated validator | Compile-to-result | 2.110–2.148 ms | 1.949–1.974 ms | 1.07–1.10× | B1173 diagnostic. Controls L/C 1.80 / 1.28%. IQR ≤2.05%. |
| Configuration manifest | Compilation only | 0.15576–0.15686 ms | 0.16836–0.17140 ms | 0.91–0.93× | B1130 diagnostic. Controls L/C 0.70 / 1.80%. IQR ≤0.80%. |
| Configuration manifest | Compile-to-result | 0.463–0.464 ms | 0.493–0.498 ms | 0.93–0.94× | B1130 diagnostic. Controls L/C 0.30 / 0.99%. IQR ≤2.07%. |
| Retained nonlocal siblings | Compilation only | 0.04323–0.04410 ms | 0.05508–0.05515 ms | 0.78–0.80× | B1109 diagnostic. Controls L/C 2.01 / 0.12%. IQR ≤2.17%. |
| Retained nonlocal siblings | Compile-to-result | 0.221 ms | 0.252–0.255 ms | 0.87–0.88× | B1109 diagnostic. Controls L/C 0.10 / 0.96%. IQR ≤1.02%. |
| Shipment report with named format selectors | Compilation only | 0.02498–0.02535 ms | 0.03346–0.03397 ms | 0.74–0.76× | B1190 diagnostic. Controls L/C 1.49 / 1.53%. IQR ≤3.25%. |
| Shipment report with named format selectors | Compile-to-result | 3.915–4.031 ms | 3.324–3.370 ms | 1.16–1.21× | B1190 diagnostic. Controls L/C 2.96 / 1.39%. IQR ≤1.29%. |
| Shipment report with positional format selectors | Compilation only | 0.02489–0.02539 ms | 0.03288–0.03296 ms | 0.76–0.77× | B1190 diagnostic. Controls L/C 2.04 / 0.26%. IQR ≤3.19%. |
| Shipment report with positional format selectors | Compile-to-result | 3.865–3.902 ms | 2.966–3.085 ms | 1.25–1.32× | B1190 diagnostic. Controls L/C 0.95 / 4.04%. IQR ≤2.83%. |

Compilation includes compiled-owner disposal. Compile-to-result includes compilation,
one execution and compiled-owner disposal. Separately warmed medians are not
additive. Warm-only numbers do not describe single-use scripts; use compile-to-result.
These collections exclude cold startup. Earlier startup measurements use separate boundaries.


## Earlier measured workloads

These rows retain the sources and uncertainties of earlier measurements.

<details>
<summary>Earlier warm, compilation and compile-to-result observations</summary>

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 1.656–1.708 ms | 0.416–0.423 ms | 3.92–4.11× | B1143 diagnostic. Controls L/C 3.15 / 1.70%. IQR ≤3.14%. Gap size uncertain. |
| CSV invoice | 4,096 data records / 127,031 B | 6.439–6.606 ms | 6.169–6.276 ms | 1.03–1.07× | B1143 diagnostic. Controls L/C 2.59 / 1.73%. IQR ≤1.56%. |
| CSV invoice | 4,096 data records / 127,031 B | 7.174–7.230 ms | 6.439–6.564 ms | 1.09–1.12× | B1114 diagnostic. Controls L/C 0.79 / 1.95%. IQR ≤3.04%. |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 1.635–1.698 ms | 0.409–0.411 ms | 3.98–4.16× | B1122 diagnostic. Controls L/C 3.86 / 0.49%. IQR ≤1.94%. Gap size uncertain. |
| Unannotated rule closures | 2,048 inputs / 4,096 definitions | 2.818–2.883 ms | 0.681 ms | 4.14–4.24× | B1057 diagnostic. Controls L/C 2.29 / 0.03%. IQR ≤0.77%. |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 1.964–2.010 ms | 0.410–0.411 ms | 4.77–4.90× | B1057 diagnostic. Controls L/C 2.37 / 0.28%. IQR ≤1.40%. |
| Captured rule closures | 2,048 inputs / 4,096 returned closures | 3.163–3.227 ms | 0.768–0.792 ms | 3.99–4.20× | B1057 diagnostic. Controls L/C 2.02 / 3.20%. IQR ≤1.30%. |
| Mutable rule counters | 2,048 counters / 4,096 calls | 1.752–1.794 ms | 0.463–0.466 ms | 3.76–3.88× | B1057 diagnostic. Controls L/C 2.41 / 0.77%. IQR ≤2.06%. |
| Configuration edit | 181 B / 16 paths | 0.0766–0.0779 ms | 0.06142–0.06219 ms | 1.23–1.27× | B1002 diagnostic. Controls L/C 1.70 / 1.25%. IQR ≤1.83%. |
| Configuration manifest | 833 B / 4 declared targets | 0.189–0.192 ms | 0.154–0.156 ms | 1.21–1.24× | Diagnostic bdab/7170. Controls L/C 1.42 / 1.22%. IQR ≤1.46%. |
| Formatted report | 3,072 records / 189,441 B | 2.934–2.942 ms | 2.671–2.737 ms | 1.07–1.10× | Diagnostic627/5ae; controls L/C 0.27/2.47%; IQR≤5.34% |
| JSON-lines record transform | 4,096 records / 110,592 B | 3.898–4.012 ms | 4.388–4.423 ms | 0.88–0.91× | Diagnostic ea8/cf; controls L/C 2.93/0.79%; IQR≤0.79% |
| Numeric CSV quality report | 4,096 records / 127,031 B | 4.066–4.166 ms | 4.574–4.693 ms | 0.87–0.91× | Diagnostic; controls L/C 2.46/2.60%; IQR≤1.47% |
| Text normalization | 52,224 B / 4,096 lines | 0.377–0.382 ms | 0.676–0.679 ms | 0.56–0.57× | Diagnostic 4ff69/74d1. Controls L/C 1.36 / 0.53%. CPython varies across collections. |
| JSON-lines aggregation with a helper | 4,096 records / 110,592 B | 3.444–3.606 ms | 4.227–4.238 ms | 0.81–0.85× | Diagnostic R534/40db; Lython control 4.69%; IQR≤2.36% |
| Floating JSON numeric report | 4,096 pairs / 56,495 B | 1.036–1.063 ms | 0.833–0.835 ms | 1.24–1.28× | Diagnostic R534/40db; controls L/C 2.56/0.33%; IQR≤1.64% |
| Helper text normalization | 52,224 B / 4,096 lines | 1.025–1.026 ms | 0.728–0.732 ms | 1.40–1.41× | B1006 diagnostic. Controls L/C 0.06 / 0.66%. IQR ≤10.08%. |
| CSV field validation | 4,128 data records / 128,183 B | 2.821–2.860 ms | 4.313–4.348 ms | 0.65–0.66× | Diagnostic c14c/1264. Controls L/C 1.41 / 0.82%. IQR ≤0.59%. |

| Workload | Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Annotated rule closures | Compilation only | 0.02233–0.02275 ms | 0.03110–0.03140 ms | 0.71–0.73× | B1143 diagnostic. Controls L/C 1.86 / 0.97%. IQR ≤2.77%. |
| Annotated rule closures | Compile-to-result | 1.695–1.769 ms | 0.443–0.455 ms | 3.73–3.99× | B1143 diagnostic. Controls L/C 4.35 / 2.56%. IQR ≤2.97%. Gap size uncertain. |
| CSV invoice | Compile-to-result | 7.391–7.406 ms | 6.710–6.804 ms | 1.09–1.10× | B1114 diagnostic. Controls L/C 0.21 / 1.40%. IQR ≤0.65%. |
| CSV invoice | Compilation only | 0.09178–0.09497 ms | 0.09994–0.10061 ms | 0.91–0.95× | B1114 diagnostic. Controls L/C 3.48 / 0.67%. IQR ≤0.97%. |
| Annotated rule closures | Compile-to-result | 1.668–1.726 ms | 0.431–0.435 ms | 3.83–4.00× | B1122 diagnostic. Controls L/C 3.43 / 0.96%. IQR ≤3.97%. |
| Annotated rule closures | Compilation only | 0.02342–0.02344 ms | 0.03100–0.03110 ms | 0.75–0.76× | B1122 diagnostic. Controls L/C 0.07 / 0.30%. IQR ≤1.11%. |
| Generated validator | Compilation only | 2.48736–2.51106 ms | 1.83120–1.83826 ms | 1.35–1.37× | B1102 diagnostic. Controls L/C 0.95 / 0.39%. IQR ≤5.01%. Gap size uncertain. |
| Generated validator | Compile-to-result | 2.635–2.642 ms | 1.967–1.968 ms | 1.34× | B1102 diagnostic. Controls L/C 0.29 / 0.08%. IQR ≤2.07%. |
| Configuration edit | Compilation only | 0.03577–0.03642 ms | 0.03409–0.03420 ms | 1.05–1.07× | B1002 diagnostic. Controls L/C 1.82 / 0.33%. IQR ≤1.21%. |
| Configuration edit | Compile-to-result | 0.1336–0.1345 ms | 0.1235–0.1260 ms | 1.06–1.09× | B1002 diagnostic. Controls L/C 0.66 / 1.98%. IQR ≤0.60%. |
| Unannotated rule closures | Compilation only | 0.03308–0.03364 ms | 0.04178–0.04211 ms | 0.79–0.81× | B1057 diagnostic. Controls L/C 1.68 / 0.81%. IQR ≤0.62%. |
| Unannotated rule closures | Compile-to-result | 2.889–2.959 ms | 0.723–0.725 ms | 3.98–4.09× | B1057 diagnostic. Controls L/C 2.43 / 0.29%. IQR ≤0.42%. |
| Annotated rule closures | Compilation only | 0.02500–0.02513 ms | 0.03079–0.03101 ms | 0.81–0.82× | B1057 diagnostic. Controls L/C 0.51 / 0.73%. IQR ≤1.49%. |
| Annotated rule closures | Compile-to-result | 1.994–2.019 ms | 0.438 ms | 4.55–4.61× | B1057 diagnostic. Controls L/C 1.24 / 0.05%. IQR ≤1.22%. |
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
| Captured rule closures | Compilation only | 0.03899–0.03917 ms | 0.04710–0.04780 ms | 0.82–0.83× | B1057 diagnostic. Controls L/C 0.46 / 1.50%. IQR ≤0.91%. |
| Captured rule closures | Compile-to-result | 3.263–3.312 ms | 0.821–0.845 ms | 3.86–4.04× | B1057 diagnostic. Controls L/C 1.49 / 2.95%. IQR ≤0.54%. |
| Mutable rule counters | Compilation only | 0.02383–0.02396 ms | 0.02936–0.02944 ms | 0.81–0.82× | B1057 diagnostic. Controls L/C 0.56 / 0.28%. IQR ≤1.36%. |
| Mutable rule counters | Compile-to-result | 1.776–1.859 ms | 0.494–0.500 ms | 3.56–3.76× | B1057 diagnostic. Controls L/C 4.67 / 1.13%. IQR ≤1.18%. |
| CSV field validation | Compilation only | 0.0250–0.0251 ms | 0.0265–0.0267 ms | 0.93–0.94× | Diagnostic c14c/1264. Controls L/C 0.29 / 0.72%. IQR ≤0.66%. |
| CSV field validation | Compile-to-result | 3.146–3.147 ms | 4.663–4.669 ms | 0.67× | Diagnostic c14c/1264. Controls L/C 0.05 / 0.12%. IQR ≤1.11%. |

B1057 measured **f80035c13 / frozen 8ce376c4** on
**2026-11-19 UTC** against stock **CPython 3.13.16** on the quiet **Azure
F4as_v7**, with Ubuntu 24.04.4 and .NET 11 RC3. Each process ran all three
phases. Lython's warm controls differ by at most 2.41% on the seven
inputs. Use the paired B1055 comparison to attribute the capture improvement.
[B1057 observations](../benchmarks/results/2026-11-19-retained-capture-cpython/README.md) ·
[Earlier B1047](../benchmarks/results/2026-11-19-retained-selectors-cpython/README.md) ·
[B1055 capture comparison](../benchmarks/results/2026-11-19-prepared-capture-slots/README.md).

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

</details>

## Changes within Lython

These are separate source-pair observations; they cannot be added or substituted
for the CPython ratios above.

| Change | Established outcome | Limits / evidence |
| --- | --- | --- |
| Derive class-cell demand in the lexical walk | Dataclass compilation allocates 11.10% less and takes 6.92–10.61% less elapsed with lower CPU. The other four scripts allocate 1.84–3.37% less during compilation. | Dataclass compile-to-result elapsed falls 0.09–4.99% with mixed CPU. Validator and manifest timing span zero. Warm allocations and accounted peaks match. [B1199](../benchmarks/results/2026-11-20-lexical-class-cells/README.md). |
| Keep `str.format` renderer state in its continuation | Both shipment scripts allocate about 7.8% less warm. The positional shipment takes 3.01–6.22% less elapsed with lower CPU. | Named shipment elapsed rises 0.08–1.02% with lower or equal CPU. Two nested widths allocate 9.24% less and span zero on both clocks. [B1198](../benchmarks/results/2026-11-20-inline-format-continuation/README.md). |
| Emit eager expressions directly | Compile allocation falls 2.33–5.37% across five scripts. Manifest compile-to-result elapsed falls 1.61–3.50% with lower process CPU. | Validator compile-to-result elapsed falls with mixed CPU. Dataclass elapsed and eager warm remain mixed. [B1197](../benchmarks/results/2026-11-20-eager-expression-emission/README.md). |
| Consolidate class suites onto executable instructions | Removes both lowered statement interpreters. Compiler buffer reuse recovers part of the cold cost and reduces manifest compile allocation by 5.98%. | The 49-class dataclass script takes 5.53–10.26% longer from compile to result and 0.88–1.75% longer warm. Its accounted execution peak matches. [B1195](../benchmarks/results/2026-11-20-executable-class-suites/refinement/README.md). |
| Emit ordinary statement suites directly | Managed allocation during compilation falls 0.11–2.45% across five workloads. With longer quiet batches validator compilation takes 9.75–13.73% less time, with lower CPU. | The first compile-only run spanned zero. Warm allocation and accounted peaks match. Eager warm timing remains mixed. [B1191](../benchmarks/results/2026-11-20-direct-statement-emission/README.md) · [B1192](../benchmarks/results/2026-11-20-direct-statement-emission/confirmation/README.md). |
| Prepare named `str.format` argument indices once | The file-backed named shipment allocates 15.69% less warm and takes 1.67–3.79% less time warm and 5.37–6.19% less from compile to result, with lower CPU. | The tested compiled report retains 224 additional bytes. The positional shipment control takes 0.12–2.45% more elapsed with mixed CPU. [B1189](../benchmarks/results/2026-11-20-prepared-format-keywords/README.md). |
| Consolidate executable function declaration metadata | Three annotation scripts retain 5.08–13.97% fewer managed bytes after compilation. A generated suite of 128 annotated definitions retains 10.89% fewer. | Isolated retained-memory diagnostic with matching warm allocation and accounted peaks. CPU and elapsed are unmeasured. [B1187](../benchmarks/results/2026-11-20-executable-definition-retention/README.md). |
| Bind unpublished keyword defaults from final value slots | Three factory scripts allocate 11.55–19.24% less with the same accounted peaks. | The quiet factory, dictionary-read and invoice timing ranges span zero. The prior binder’s CPU increases remain in B1182. [B1183](../benchmarks/results/2026-11-20-prepared-keyword-default-binding/README.md). |
| Reuse evaluated dictionary display keys | Expanded forwarding takes 13.66–14.24% less warm elapsed and 12.62–15.38% less CPU, with 31.75% less managed allocation. | Rebuilt-key elapsed rises 0.20–0.51%. The initial adverse invoice result does not repeat in the invoice-only run. [B1179](../benchmarks/results/2026-11-20-dynamic-record-layout/README.md). |
| Keep evaluated numeric call operands in their slots | The in-memory invoice removes 24.91% of warm allocation and takes 2.24–4.43% less elapsed time. | Eager rule and file-backed invoice timing spans zero. [B1178](../benchmarks/results/2026-11-20-call-operand-publication/README.md). |
| Prepared executable lambda bindings | A generated table of 128 lambdas retains 37.39% fewer managed bytes after compilation. A stable sort with one key lambda retains 5.66% fewer. | `9b14997e4` versus `5f42f319c`, retained at `68215bac8`. Warm allocation is flat for both workloads. Retained bytes were measured in isolated processes after collection. [Lambda binding diagnostic](../benchmarks/results/2026-11-20-executable-lambda-binding/README.md). |
| Combined function and annotation admission | Eager compile-to-result improves 3.54–5.39%. Reading annotations each definition improves 2.31–5.88% warm. Both use less CPU. | `ab2aa9554` versus `02639bec8`, retained at `f3b9957bd`. Local allocation and peaks stay flat. Eager warm has adverse pairings and 3.94% candidate spread. [B1169](../benchmarks/results/2026-11-20-funded-definition-construction/README.md). |
| Direct generated dataclass defaults | The inherited dataclass script runs 7.75–11.67% faster warm and 5.26–7.39% faster including compilation, with lower CPU. Warm allocation falls 12.25%. | `5908b8c94` versus `2b8e388a1`, retained at `4e2bd4695`. The accounted peak rises by 384 bytes. Registry control CPU rises 0.53–1.49% with mixed elapsed. [B1167](../benchmarks/results/2026-11-20-generated-dataclass-defaults/README.md). |
| Publish prepared annotation dictionaries on first read | Nested factories run 7.08–7.55% faster warm with 4.95–7.17% lower CPU. Eager and nested rules allocate 13.65% and 10.35% less, respectively. | `e6e1f964c` versus `cf8b7ab63`, delivered as `cb08bb8a2`. Accounted peaks match. Timing for eager rules and the read-every-definition control spans zero. [B1163](../benchmarks/results/2026-11-20-owned-annotation-values/README.md). |
| Direct method-format operands | Warm allocation falls 8.65% in expanded forwarding, 1.75% in shipment selectors and 4.12% in named formatting, with the same accounted peaks. Forwarding warm elapsed falls 3.58–4.59% with lower CPU. | `f1593aaf5` versus `95654712a`. The unchanged invoice also runs faster, which limits attribution. Shipment warm elapsed and CPU span zero. [B1160](../benchmarks/results/2026-11-20-method-format-operands/README.md). |
| Direct class header handoff | The expanded registry runs 2.65–3.93% faster warm with lower CPU and 5.72% less allocation. Inherited dataclass warm allocation falls 1.99%. | `0bb3783a2` versus `c3f229508`. Inherited dataclass warm elapsed has a 7.44% candidate spread. Its compile-to-result CPU rises 0.22–3.64% with flat elapsed. [B1158](../benchmarks/results/2026-11-20-direct-class-headers/README.md). |
| Shared bound invocation entry | JSON hook rules run 6.43–10.91% faster warm and 8.68–9.46% faster including compilation. Record sorting improves 0.49–3.43% warm. Both use less CPU. Warm allocation falls 28.84% and 21.66%. | `9bdd10a70` versus `87a269f2b`. Eager annotated rules also use less CPU. Captured rules and invoice timings are inconclusive. Accounted peaks match. [B1157](../benchmarks/results/2026-11-20-shared-bound-entry/README.md). |
| Compiled postponed annotations | Postponed factory scripts run 16.68–22.72% faster warm with lower CPU and 41.14–47.97% less allocation. The repeated-definition script reaches its result 23.11–24.29% sooner including compilation. | `e025a7f1c` versus `a6928c942`. Compile allocation rises 0.47–3.09%. Single-definition elapsed is inconclusive, eager control CPU rises, and the unchanged invoice workload runs 0.97–1.28% slower with mixed CPU. [B1155](../benchmarks/results/2026-11-20-compiled-postponed-annotations/README.md). |
| Shared postfix statement parsing | Validator compiles 11.81–15.64% faster, reaches its result 11.94–13.02% sooner and allocates 20.58% less during compilation. Manifest compilation is 8.94–10.62% faster. | `42055debe` versus parser source retained as `573cac2c6`. CSV warm elapsed is inconclusive and CPU rises 0.64–2.74%. [B1129](../benchmarks/results/2026-11-19-postfix-statement-parsing/README.md). |
| Native integer failures and short results | The validator with six malformed fields improves 18.81–20.45% warm with 14.22–14.26% lower process CPU and 4.81% less allocation. The all-valid validator improves 0.70–2.01% warm with lower CPU. | `365335ea8` versus `e20604ef4`, both with the same admitted error renderer. Manifest, CSV and ordinary rule timing spans zero. [B1135](../benchmarks/results/2026-11-19-native-integer-transport/README.md). |
| Prepared CSV dialect searches | Warm elapsed falls 1.70–5.41% on invoice, 3.44–9.62% on keyword invoice and 2.43–4.05% on long-field validation. | `fdacfbe20` versus prototype `0c52b0956`, retained in `4c3f1ddfa` with the escaped-newline correction. Keyword invoice and long-field validation use less CPU. The unrelated text control spans zero. Accounted CSV peaks rise 512 bytes. [B1141](../benchmarks/results/2026-11-19-prepared-csv-search-sets/README.md). |
| Inline function binding state | Warm allocation falls 3.20–6.13% across rule scripts. The annotated compile-to-result repeat is 2.12–3.01% faster with lower CPU. | `65926a62d` versus prototype `757501cbe`, retained at `353ec074c` with CSV search preparation. Captured warm elapsed is inconclusive and the invoice repeat uses 1.35–2.00% more CPU. [B1142](../benchmarks/results/2026-11-19-inline-function-binding-state/README.md). |
| Reuse selected-path compiler bindings | Report compilation is 1.30–3.98% faster with lower CPU and 3.40% less allocation. Validator compile-to-result is 0.22–2.99% faster. | `ee057fd12` versus prototype `5828dad90`, retained at `5784352e4`. Validator compile-only and the warm report control span zero. [B1144](../benchmarks/results/2026-11-19-selected-abstract-paths/README.md). |
| Source-selected annotations | Annotated compile-to-result is 2.52–8.42% faster. Reading each annotation dictionary is 3.40–5.98% faster warm with lower CPU. Warm allocation in the two annotated scripts falls 3.16–3.30%. | `802c7130e` versus prototype `c37f96139`, retained at `a050a9e19`. The compile-to-result control spread limits precision. Other warm CPU spans zero. [B1145](../benchmarks/results/2026-11-19-source-annotation-mode/README.md). |
| Live keyword-default lookup | Annotated rules improve 2.20–6.78% warm and reused defaults 4.02–9.86% in the repeat, both with lower CPU. | `c76c59159` versus `a7b43ff7c`, retained at `684130bc6`. The invoice control is 1.24–3.24% slower in the repeat. [B1146 and B1147](../benchmarks/results/2026-11-19-keyword-default-record/README.md). |
| Shared keys in forwarded records | JSON and CSV keyword-forwarding variants run 5.60–6.89% and 3.84–6.18% faster warm, with lower CPU and 16.72% and 23.15% less local allocation. | `630101daf` versus source retained at `647a0f9b5`. Positional CSV control CPU rises 0.62–2.78%. [B1123](../benchmarks/results/2026-11-19-shared-record-keywords/README.md). |
| Inline ASCII trim membership | Expanded forwarding runs 3.06–3.39% faster warm and 1.09–3.45% faster compile-to-result. Mixed Unicode trim improves 14.07–14.78% warm. Both use less CPU and allocate less. | `43c9f2c38` versus `12fabffb1`. Shipment JSON elapsed and CPU span zero. Earlier adverse controls remain in B1063. [B1064](../benchmarks/results/2026-11-19-trim-policy-scan/README.md). |
| Independent capture cells | Captured rules improve 0.78–2.84% warm and 0.95–2.69% compile-to-result, with lower CPU. Allocation falls 6.22% and accounted peak falls 46.86%. | `56d7e329d` versus `68a7fc465`. Annotated warm elapsed stays within 0.18%, with CPU up to 2.40% higher. [B1053](../benchmarks/results/2026-11-19-standalone-capture-cells/README.md). |
| Prepared capture sources | Captured rules improve 6.05–7.42% warm and 4.12–5.16% compile-to-result. Mutable counters improve 7.23–9.89% warm and 5.24–9.66% compile-to-result. Both allocate less and use less CPU. | `68a7fc465` versus `f80035c13`. Forwarding and positional elapsed controls span zero. [B1055](../benchmarks/results/2026-11-19-prepared-capture-slots/README.md). |
| Shared compiler bindings | Captured and generic rules compile 2.17–3.78% and 1.03–3.77% faster with lower CPU and 3.23% and 4.15% less allocation respectively. | `f80035c13` versus `d4b2088d4`. Captured and mutable compile-to-result span zero. [B1058](../benchmarks/results/2026-11-19-prepared-compiler-bindings/README.md). |
| Independently owned mutable cells | Retained nonlocal siblings run 1.32–5.74% faster warm and 2.63–4.53% faster compile-to-result. Accounted peak falls 37.03% for siblings and 56.09% for mutable counters. | `d4b2088d4` versus `43c9f2c38`. Mutable-counter elapsed spans zero. Forwarding compile-to-result is 0.21–1.95% slower with mixed CPU. [B1062](../benchmarks/results/2026-11-19-detached-nonlocal-cells/README.md). |
| Direct singleton capture | Retained siblings improve 2.38–4.62% compile-to-result with lower CPU and 6.39% less allocation. Captured rules allocate 2.76% less. | `12fabffb1` versus `773be8dc2`. Both targets' warm elapsed spans zero. Three of four captured-rule warm pairs are slower. [B1074](../benchmarks/results/2026-11-19-direct-capture-access/README.md). |
| Single prepared-definition dispatch | Ordinary rules improve 0.70–2.87% warm with 3.06–4.68% lower process CPU in the longer-warmup confirmation. | `773be8dc2` versus source-equivalent candidate retained as `eb444b988`. The initial ordinary timings were unstable. Allocation and accounted peaks match. [Initial B1075](../benchmarks/results/2026-11-19-prepared-definition-dispatch/README.md) · [Confirmation B1076](../benchmarks/results/2026-11-19-prepared-dispatch-confirmation/README.md). |
| Local factory frame removal | Ordinary rules improve 2.91–6.35% warm and 3.79–5.92% compile-to-result. Annotated rules improve 8.49–9.84% warm and 7.11–8.29% compile-to-result. Both use less CPU and allocate 884,736 fewer bytes per script. | `aa03ef5bc` versus source-equivalent candidate retained as `1c9a8e6a1`. Accounted peaks match. Captured, positional and forwarding elapsed span zero. Invoice CPU rises 0.64–1.37%. [B1079](../benchmarks/results/2026-11-19-deferred-factory-definitions/README.md). |
| Compiled definition stores | Captured rules improve 1.00–5.51% warm, registration improves 0.52–3.69% warm and annotated rules improve 1.57–3.53% compile-to-result. CPU falls on all three. | `896e0c40a` versus source-equivalent candidate retained as `44fe04178`. Allocation during compilation rises 0.04–1.14%. Forwarding compile-to-result is 0.14–1.34% slower with mixed CPU. [B1082](../benchmarks/results/2026-11-19-compiled-definition-publication/README.md). |
| Inline keyword dictionaries | Dynamic-key and literal-key reports improve 3.96–5.48% and 10.09–12.95% warm with lower CPU. Local allocation falls 21.09% and 24.58%. | The isolated change retained as `c3cb1906a` transfers a private `**{...}` mapping. The named forwarding control is 0.80–2.78% slower warm with higher CPU. [B1090](../benchmarks/results/2026-11-19-private-keyword-mapping/README.md). |
| Shared charge for captured cells | Captured rules improve 2.13–3.76% warm with 2.72–4.32% lower CPU and 0.91–1.71% lower compile-to-result elapsed. | `06b40a712` versus the isolated change retained as `2ba3105c3`. Allocation is unchanged. Annotated and forwarding warm elapsed rise 0.61–2.65% and 0.60–0.99%. [B1096](../benchmarks/results/2026-11-19-funded-capture-transfer/README.md). |
| Ordered declaration preparation | Rule compilation improves 2.92–6.01% across three scripts with lower CPU and 3.41–4.02% less allocation. Captured compile-to-result improves 1.77–3.54% with lower CPU. | `482b45a89` versus source-equivalent `48a4fca4a`. Validator compilation is 0.77–5.56% slower with mixed CPU and divergent candidate processes. [B1098](../benchmarks/results/2026-11-19-child-declaration-preparation/refinement/README.md). |
| Final positional default tuple | Ordinary and annotated rules allocate 3.62% and 2.89% less. Warm rule elapsed is inconclusive for the retained source. | Source-equivalent `f8a7920e7` versus `accd3c2d5`. Accounted peaks rise 5.95% and 3.47%. Computed-default CPU falls under `RunAsync` and rises 0.69–1.97% synchronously. [B1101](../benchmarks/results/2026-11-19-positional-default-slots/refinement/README.md). |
| Source positions during tokenization | Validator compile-to-result improves 6.00–7.56% with 5.27–7.96% lower process CPU. Manifest compilation improves 1.27–3.44%. | Source-equivalent `80cbf4988` versus `27f68744b`. Local compilation allocation rises 1.77% for the validator and 1.59% for the manifest. The warm shipment control is inconclusive. [B1105](../benchmarks/results/2026-11-19-token-source-transport/README.md). |
| Factories with standalone captures | Retained sibling closures improve 14.12–18.15% warm with lower CPU and 32.99% less allocation. Captured rules allocate 20.02% less and improve 0.74–2.84% compile-to-result. | `2166c7295` versus `9407f4fb7`. Accounted peaks match. Ordinary-rule warm CPU rises 0.52–3.05%. [B1107](../benchmarks/results/2026-11-19-standalone-capture-prefix/README.md). |
| Right-trimmed ranges in native split | Named forwarding improves 5.34–7.24% warm and 3.32–5.11% compile-to-result. Inline forwarding improves 4.16–8.03% warm. Both use less CPU and allocate 2.11% and 2.67% less respectively. | `9407f4fb7` versus `3e67fd010`. Both accounted peaks fall 131,104 bytes. CSV invoice elapsed is inconclusive and process CPU rises 0.62–2.74%. [B1108](../benchmarks/results/2026-11-19-trim-range-split/README.md). |
| Final default containers with direct result forwarding | Ordinary rules allocate 8.75% less and improve 1.61–2.53% warm and 1.68–2.99% compile-to-result with lower CPU. Annotated rules allocate 6.94% less and use less CPU with inconclusive elapsed. | `3e67fd010` versus source-equivalent `8ffd40dc0`. Asynchronous computed defaults take 0.14–1.16% longer with mixed CPU. The initial ordinary-rule regression remains in B1111. [B1112](../benchmarks/results/2026-11-19-direct-default-payload/refinement/README.md). |
| Fixed tuple returns from executable frames | Captured rules allocate 6.95% less and improve 3.09–5.60% warm with 2.48–4.00% lower CPU. The accounted peak matches. | `e1636b271` versus `2841f6ac2` using source-equivalent images. Captured compile-to-result elapsed and ordinary-rule controls span zero. One ordinary confirmation block shows 0.00–5.32% higher CPU. [B1113](../benchmarks/results/2026-11-19-framed-unpacked-return/README.md) · [Repeat](../benchmarks/results/2026-11-19-framed-unpacked-return/confirmation/README.md). |
| Borrowed lexical leaf calls and fixed returns | Captured rules improve 8.11–13.63% warm and 7.05–9.86% compile-to-result with lower CPU and 26.90% less allocation. Ordinary rules improve 2.82–6.63% warm with lower CPU. | `d0fc1bb93` versus `7fd3a5af3`, with the call changes retained at `47fbd2c1b`. Annotated control timings remain mixed, including up to 5.08% higher CPU in one repeat block. [B1117](../benchmarks/results/2026-11-19-inline-lexical-entry/README.md) · [Repeat](../benchmarks/results/2026-11-19-inline-lexical-entry/confirmation/README.md). |
| Compiled qualified names and closed leaf ownership | Rule-registration report improves 8.57–11.94% warm and 6.66–8.33% compile-to-result, with lower CPU. Allocation falls 18.18%. The two ordinary rule scripts allocate 5.01% and 6.36% less. | `c8e749318` versus `56d7e329d`. Annotated-rule, forwarding and positional elapsed span zero on repeat. The initial 23.95% annotated-warm candidate spread remains in the evidence. [B1051/B1052](../benchmarks/results/2026-11-19-compiled-qualified-names/README.md). |
| Prepared native format keys | Shipment report warm elapsed improves 13.17–13.75% and compile-to-result improves 13.87–14.21%, both with lower CPU. Allocation falls 10.80% and accounted peak falls 25.91%. | `014e58531` versus `9ac02d15d`. Ordinary method-format elapsed and CPU span zero on repeat. [B1045/B1046](../benchmarks/results/2026-11-19-native-format-selectors/README.md). |
| Compiled declaration text | Rule warm elapsed improves 2.96–3.13% and 3.42–6.69% with lower CPU. Allocation falls 5.15% and 5.47%. A documented rule variant improves 8.48–13.30% warm and allocates 14.37% less. | Compile-to-result CPU improves on repeat for both ordinary rule scripts. Positional warm elapsed rises 0.56–1.63% on repeat with mixed CPU. [B1041/B1042](../benchmarks/results/2026-11-19-prepared-definition-text/README.md). |
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
| Native helper returns | Warm allocation falls 19.92% for CSV helpers and 19.31% for JSON helpers. | Release, stress and installed consumers qualified at `8f07764a`. The original elapsed improvements and slower text control remain diagnostic. [Source pair and qualification](../benchmarks/results/2026-11-02-native-function-returns/README.md). |
| Direct UTF-8 formatting/storage | Allocation8.67% lower; controlled report elapsed/CPU5–8% lower supports retention | Original adverse15.125ms remains unexplained. [Original/confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md). |
| Floating arithmetic-to-iteration transport | Warm/fresh elapsed23–27% lower with supporting CPU; allocation28.52% lower | Historical source pair; integer controls uncertain, original regression retained. [Evidence](../benchmarks/results/2026-11-17-float-transport/README.md). |
| Shared receiver calls | Helper text/report warm24–40% lower, fresh22–39% lower, supporting CPU | Historical workloads/source pair; numeric magnitude uncertain. [Evidence](../benchmarks/results/2026-11-17-receiver-calls/README.md). |
| Bulk CSV/shared-key/final-row construction | Earlier complete-script6–8% improvement is already integrated | Not new pending work. Detailed other improvements/debts remain in the [previous guide](../benchmarks/results/2026-11-17-current-cpython-checkpoint/prior-BENCHMARKS.md) and [history](../benchmarks/EXPERIMENTS.md). |

Rejected optimizations and qualification narratives stay behind the evidence links.
Use the [existing tooling](../benchmarks/README.md); keep current benchmark status
and source revision here. Measurements and qualification do not authorize publication.
