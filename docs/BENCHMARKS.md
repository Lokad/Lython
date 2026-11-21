# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV restock export | 3,072 records / 182,437 B | 3.759–3.907 ms | 5.102–5.129 ms | 0.733–0.766× | B1278 diagnostic. Controls L/C 3.92 / 0.52%. IQR ≤0.80%. |
| CSV restock export with Unicode escaping | 3,072 records / 170,917 B | 4.507–4.525 ms | 5.714–5.798 ms | 0.777–0.792× | B1278 diagnostic. Controls L/C 0.41 / 1.48%. IQR ≤0.78%. |
| JSON Lines export with expanded options | 3,072 records / 189,441 B | 6.503–6.598 ms | 6.534–6.571 ms | 0.990–1.010× | B1261 diagnostic. Spans parity. Controls L/C 1.47 / 0.57%. IQR ≤1.78%. |
| Map-based text report | 3,072 lines / 35,840 B | 1.440–1.495 ms | 1.217–1.220 ms | 1.181–1.228× | B1274 diagnostic. Controls L/C 3.84 / 0.20%. IQR ≤0.75%. |
| JSON normalization | 3,072 records / 189,441 B | 2.762–2.813 ms | 2.505–2.544 ms | 1.086–1.123× | B1253 diagnostic repeat. Initial B1252 is 1.143–1.158×. Repeat controls L/C 1.82 / 1.59%. IQR ≤12.50%. |
| Unannotated rule closures | 2,048 inputs / 4,096 definitions | 2.307–2.321 ms | 0.683–0.688 ms | 3.35–3.40× | B1122 diagnostic. Controls L/C 0.61 / 0.70%. IQR ≤0.74%. |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 1.057 ms | 0.428–0.434 ms | 2.44–2.47× | B1203 diagnostic. Controls L/C 0.05 / 1.26%. IQR ≤2.76%. |
| Nested annotated factories | 2,048 inputs / 4,096 definitions | 1.460–1.506 ms | 0.623–0.632 ms | 2.31–2.42× | B1229 diagnostic. Controls L/C 3.16 / 1.51%. IQR ≤2.22%. |
| Postponed annotated rules | 2,048 inputs / 2,048 definitions | 0.967–0.975 ms | 0.397–0.398 ms | 2.43–2.46× | B1203 diagnostic. Controls L/C 0.79 / 0.47%. IQR ≤1.48%. |
| Postponed nested factories | 2,048 inputs / 4,096 definitions | 1.163–1.214 ms | 0.5362–0.5376 ms | 2.16–2.26× | B1240 diagnostic. Controls L/C 4.35 / 0.27%. IQR ≤2.72%. |
| Captured rule closures | 2,048 inputs / 4,096 returned closures | 2.437–2.454 ms | 0.776–0.790 ms | 3.09–3.16× | B1122 diagnostic. Controls L/C 0.71 / 1.69%. IQR ≤1.69%. |
| Inline forwarding with dynamic keys | 1,024 records / 26,112 B | 1.894–1.913 ms | 1.139–1.159 ms | 1.63–1.68× | B1102 diagnostic. Controls L/C 1.01 / 1.74%. IQR ≤2.35%. |
| Expanded forwarding report | 1,024 records / 26,112 B | 1.533–1.543 ms | 1.173–1.189 ms | 1.29–1.32× | B1180 diagnostic. Controls L/C 0.62 / 1.38%. IQR ≤4.03%. |
| CSV invoice | 4,096 data records / 127,031 B | 6.877–7.224 ms | 6.588–6.618 ms | 1.04–1.10× | B1229 diagnostic. Controls L/C 5.04 / 0.45%. IQR ≤1.30%. |
| Retained nonlocal siblings | 240 factories / 720 returned closures | 0.169–0.170 ms | 0.188–0.190 ms | 0.89–0.90× | B1109 diagnostic. Controls L/C 0.57 / 0.89%. IQR ≤2.72%. |
| Shipment report with named format selectors | 3,072 records / 189,441 B | 3.732–3.755 ms | 3.357–3.410 ms | 1.09–1.12× | B1233 diagnostic. Controls L/C 0.62 / 1.56%. IQR ≤2.71%. |
| Shipment report using `str.format_map` | 3,072 records / 189,441 B | 3.622–3.724 ms | 2.851–2.900 ms | 1.25–1.31× | B1240 diagnostic. Controls L/C 2.83 / 1.72%. IQR ≤4.94%. |
| Shipment report with wide records | 3,072 records / 1,329,057 B | 6.504–6.536 ms | 12.600–13.123 ms | 0.496–0.519× | B1233 diagnostic. Controls L/C 0.49 / 4.15%. IQR ≤4.60%. |
| Shipment report with positional format selectors | 3,072 records / 189,441 B | 3.737–3.750 ms | 2.861–2.893 ms | 1.29–1.31× | B1190 diagnostic. Controls L/C 0.35 / 1.13%. IQR ≤3.50%. |
| Generated validator | 96 guards / 1,714 B JSON | 0.08372–0.08402 ms | 0.04481–0.04509 ms | 1.86–1.88× | B1213 diagnostic. Controls L/C 0.36 / 0.62%. IQR ≤2.70%. |
| Generated validator, successful integer parses | 96 guards / 1,714 B JSON | 0.07360–0.07384 ms | 0.04048–0.04121 ms | 1.786–1.824× | B1271 diagnostic. Controls L/C 0.32 / 1.81%. IQR ≤0.75%. |
| Shipment report with alternating record shapes | 3,072 records / 759,222 B | 8.840–8.953 ms | 6.564–6.674 ms | 1.325–1.364× | B1248 diagnostic. Controls L/C 1.28 / 1.68%. IQR ≤4.68%. |
| Inherited dataclass definitions | 1 base and 48 subclasses, 48 instances | 0.198–0.204 ms | 4.881–5.089 ms | 0.039–0.042× | B1222 diagnostic. Controls L/C 3.19 / 4.26%. Includes class construction and decoration. |
| Configuration manifest | 4 targets / 833 B across 2 files | 0.19562–0.19898 ms | 0.15924–0.16073 ms | 1.22–1.25× | B1222 diagnostic. Controls L/C 1.72 / 0.94%. IQR ≤3.03%. |

**Ratio = Lython elapsed / CPython elapsed. Above one means slower.** Ranges cover
the combinations of two process medians per engine, not confidence intervals.
Controls L/C report the difference between the two Lython/CPython medians.

B1278 measures the CSV restock exports at **9bf720efa**,
production-equivalent to **7cc150d96**, against **CPython 3.13.16**
on **2026-11-21 UTC**. All 112 batches passed on the quiet Azure
F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3. Separate phase workers
measure compilation and time to result for the ordinary export.
[B1278 checkpoint](../benchmarks/results/2026-11-21-cpython-csv-restock-checkpoint/README.md).

B1274 measures the map-based text report at **b140244f0**,
production-equivalent to **b42c4d508**, against **CPython 3.13.16**
on **2026-11-21 UTC**. Independent phase workers passed all 84
batches on the quiet Azure F4as_v7 running Ubuntu 24.04.4 and
.NET 11 RC3.
[B1274 checkpoint](../benchmarks/results/2026-11-21-cpython-casing-checkpoint/README.md).

B1271 measures the validator with successful integer parses at **406388271**,
production-equivalent to **6e972677e**, against **CPython 3.13.16** on
**2026-11-21 UTC**. Independent phase workers completed all 84 batches on
the quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3. Earlier
validator observations remain at their measured revisions.
[B1271 checkpoint](../benchmarks/results/2026-11-21-cpython-compiler-checkpoint/README.md)
· [B1252 warm](../benchmarks/results/2026-11-20-cpython-json-normalization/README.md)
· [B1248 compilation and time to result](../benchmarks/results/2026-11-20-cpython-native-text-dict-keys/README.md).

B1261 measures the expanded-options JSON Lines export at **3ab6f6c81**,
with the runtime change retained at `8785193bb`, against **CPython 3.13.16**
on **2026-11-21 UTC**. Independent warm, compilation and compile-to-result
workers completed all 84 batches on the quiet Azure F4as_v7.
[B1261 observations](../benchmarks/results/2026-11-21-cpython-expanded-builtin/README.md).

B1258 measures the map-based text report at **8ad55d9d5**, with the
callback change retained at `d6a360e23`, against **CPython 3.13.16** on
**2026-11-21 UTC**. The quiet Azure F4as_v7 uses separate processes for
warm, compile-to-result and compilation lanes. All 84 batches passed.
[B1258 observations](../benchmarks/results/2026-11-21-cpython-callback-text/README.md).

B1252 and B1253 measure JSON normalization at **5901c06bd**, retained
as `ea04028b5` alongside the regex correction, against **CPython
3.13.16** on **2026-11-20 UTC**. The confirmation separates phase
workers to check the variable initial compile-to-result result.
B1252 also measures the warm validator. Both collections used the
quiet Azure F4as_v7 and passed all 168 batches. [B1252/B1253 observations](../benchmarks/results/2026-11-20-cpython-json-normalization/README.md).

B1248 measures the validator without malformed fields and the alternating
shipment at **c62457d49** against **CPython 3.13.16** on **2026-11-20 UTC**.
Both quiet gates and all 112 batches passed. The validator also measures
compilation and compile-to-result. [B1248 observations](../benchmarks/results/2026-11-20-cpython-native-text-dict-keys/README.md).

B1240 measures postponed nested factories and the mapping shipment at
**785640f8e** on **2026-11-20 UTC**. Both machine checks and all 112
batches passed. The factory also measures compilation and compile-to-result.
[B1240 observations](../benchmarks/results/2026-11-20-cpython-postponed-annotation-payload/README.md).

B1233 measures the named and wide shipment reports at
**d61cfc85b / frozen 11de545f6** on **2026-11-20 UTC**. The wide records
have 24 fields each. Both machine checks and all 80 batches passed.
Only the named report also measures compilation and compile-to-result.

B1229 measures nested factories and the warm invoice at
**46ccc2efa / frozen eaf4d65c1** on **2026-11-20 UTC**. Both machine
checks and all 100 batches passed. The warm invoice has a 5.04% Lython
control spread. Eager nested compile and compile-to-result remain at B1208.

B1222 measures inherited dataclasses and the warm manifest at
**72a1426dd / frozen 842c2c8f9** on **2026-11-20 UTC**. Both machine
checks and all 80 batches passed.

B1213 measures the generated validator across all three boundaries and the
earlier warm manifest at **645a0fcd1 / frozen 356a93e25** on **2026-11-20 UTC**.
Both machine checks and all 80 batches passed. The manifest compilation
and compile-to-result rows remain at B1130.

B1208 measures earlier nested factories across warm, compilation and
compile-to-result, plus warm invoice at **ef171a35b / frozen 7f583edfa**
on **2026-11-20 UTC**. Both machine checks and all 80 batches passed.
The nested warm and compile-to-result controls vary by 8.04% and 8.50%.
B1203 measures eager, nested and postponed factories and warm invoice at
**50d63cf31 / frozen 32c5f2ef7** on **2026-11-20 UTC**. Both machine checks
and all 120 batches passed. B1200 measures earlier inherited dataclasses and the
earlier warm eager rules and invoice at
**f65be4da5 / frozen 537b4b3dd** on **2026-11-20 UTC**. Both machine checks
and all 100 batches passed. B1196 recorded the prior class-suite checkpoint
at **53d3b45d5 / frozen cf059b2a2**. B1190 measures both shipment variants and the
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
B1130 measures the earlier manifest at **a472eb2a4 / frozen e18ee460**.
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
[B1233 observations](../benchmarks/results/2026-11-20-cpython-json-property-layout/README.md) ·
[B1229 observations](../benchmarks/results/2026-11-20-cpython-prepared-entry-progress/README.md) ·
[B1222 observations](../benchmarks/results/2026-11-20-cpython-direct-declaration-operands/README.md) ·
[B1218 observations](../benchmarks/results/2026-11-20-cpython-default-operand-storage/README.md) ·
[B1213 observations](../benchmarks/results/2026-11-20-cpython-shared-executable-graph/README.md) ·
[B1208 observations](../benchmarks/results/2026-11-20-cpython-inline-captured-factories/README.md) ·
[B1203 observations](../benchmarks/results/2026-11-20-cpython-inline-lexical-definitions/README.md) ·
[B1200 observations](../benchmarks/results/2026-11-20-cpython-lexical-class-cells/README.md) ·
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
| CSV restock export | Compilation only | 0.02747–0.02770 ms | 0.04536–0.04552 ms | 0.604–0.611× | B1278 diagnostic. Controls L/C 0.83 / 0.35%. IQR ≤1.11%. |
| CSV restock export | Compile-to-result | 3.961–3.967 ms | 5.221–5.225 ms | 0.758–0.760× | B1278 diagnostic. Controls L/C 0.15 / 0.06%. IQR ≤1.84%. |
| JSON Lines export with expanded options | Compilation only | 0.02975–0.02978 ms | 0.04436–0.04453 ms | 0.668–0.671× | B1261 diagnostic. Controls L/C 0.10 / 0.38%. IQR ≤0.78%. |
| JSON Lines export with expanded options | Compile-to-result | 6.619–6.621 ms | 6.603–6.686 ms | 0.990–1.003× | B1261 diagnostic. Spans parity. Controls L/C 0.03 / 1.26%. IQR ≤1.09%. |
| Map-based text report | Compilation only | 0.02582–0.02682 ms | 0.04141–0.04188 ms | 0.616–0.648× | B1274 diagnostic. Controls L/C 3.89 / 1.15%. IQR ≤0.57%. |
| Map-based text report | Compile-to-result | 1.505–1.580 ms | 1.291–1.316 ms | 1.144–1.223× | B1274 diagnostic. Controls L/C 4.94 / 1.94%. IQR ≤0.83%. |
| JSON normalization | Compilation only | 0.03536–0.03571 ms | 0.05254–0.05284 ms | 0.669–0.680× | B1252 diagnostic. Controls L/C 1.01 / 0.57%. IQR ≤0.86%. |
| JSON normalization | Compile-to-result | 2.771–2.801 ms | 2.813–3.007 ms | 0.922–0.996× | B1253 diagnostic. The initial lane spans parity. Repeat CPython spread 6.88% and IQR up to 13.99%. |
| Unannotated rule closures | Compilation only | 0.03072–0.03102 ms | 0.04189–0.04201 ms | 0.73–0.74× | B1122 diagnostic. Controls L/C 1.00 / 0.30%. IQR ≤0.44%. |
| Unannotated rule closures | Compile-to-result | 2.372–2.375 ms | 0.728–0.729 ms | 3.25–3.26× | B1122 diagnostic. Controls L/C 0.13 / 0.20%. IQR ≤1.60%. |
| Annotated rule closures | Compilation only | 0.01992–0.02021 ms | 0.03225–0.03236 ms | 0.615–0.627× | B1203 diagnostic. Controls L/C 1.49 / 0.36%. IQR ≤1.10%. |
| Annotated rule closures | Compile-to-result | 1.086–1.088 ms | 0.462–0.471 ms | 2.31–2.36× | B1203 diagnostic. Controls L/C 0.11 / 1.92%. IQR ≤1.83%. |
| Nested annotated factories | Compilation only | 0.02403–0.02431 ms | 0.04001–0.04029 ms | 0.596–0.608× | B1208 diagnostic. Controls L/C 1.16 / 0.71%. IQR ≤1.45%. |
| Nested annotated factories | Compile-to-result | 1.410–1.530 ms | 0.638–0.642 ms | 2.20–2.40× | B1208 diagnostic. Controls L/C 8.50 / 0.57%. IQR ≤1.38%. |
| Inherited dataclass definitions | Compilation only | 0.20160–0.20564 ms | 0.46393–0.46557 ms | 0.433–0.443× | B1222 diagnostic. Controls L/C 2.00 / 0.35%. IQR ≤2.19%. |
| Inherited dataclass definitions | Compile-to-result | 0.420–0.430 ms | 5.496–5.725 ms | 0.073–0.078× | B1222 diagnostic. Controls L/C 2.37 / 4.17%. IQR ≤1.71%. |
| Postponed annotated rules | Compilation only | 0.02514–0.02533 ms | 0.03518–0.03539 ms | 0.71–0.72× | B1156 diagnostic. Controls L/C 0.77 / 0.58%. IQR ≤1.42%. |
| Postponed annotated rules | Compile-to-result | 1.531–1.599 ms | 0.429–0.436 ms | 3.51–3.73× | B1156 diagnostic. Controls L/C 4.44 / 1.68%. IQR ≤2.52%. |
| Postponed nested factories | Compilation only | 0.02519–0.02552 ms | 0.04000–0.04005 ms | 0.629–0.638× | B1240 diagnostic. Controls L/C 1.30 / 0.14%. IQR ≤1.01%. |
| Postponed nested factories | Compile-to-result | 1.195–1.241 ms | 0.5782–0.5810 ms | 2.06–2.15× | B1240 diagnostic. Controls L/C 3.83 / 0.49%. IQR ≤0.69%. |
| Captured rule closures | Compilation only | 0.03460–0.03524 ms | 0.04742–0.04748 ms | 0.73–0.74× | B1122 diagnostic. Controls L/C 1.86 / 0.14%. IQR ≤1.31%. |
| Captured rule closures | Compile-to-result | 2.519–2.523 ms | 0.830–0.839 ms | 3.00–3.04× | B1122 diagnostic. Controls L/C 0.15 / 1.09%. IQR ≤1.51%. |
| Inline forwarding with dynamic keys | Compilation only | 0.05339–0.05370 ms | 0.06049–0.06111 ms | 0.87–0.89× | B1102 diagnostic. Controls L/C 0.57 / 1.01%. IQR ≤1.53%. |
| Inline forwarding with dynamic keys | Compile-to-result | 2.003–2.012 ms | 1.285–1.298 ms | 1.54–1.57× | B1102 diagnostic. Controls L/C 0.47 / 1.00%. IQR ≤1.66%. |
| Expanded forwarding report | Compilation only | 0.04385–0.04448 ms | 0.06131–0.06261 ms | 0.70–0.73× | B1180 diagnostic. Controls L/C 1.45 / 2.12%. IQR ≤4.25%. |
| Expanded forwarding report | Compile-to-result | 1.611–1.638 ms | 1.318 ms | 1.22–1.24× | B1180 diagnostic. Controls L/C 1.65 / 0.00%. IQR ≤3.60%. |
| CSV invoice | Compilation only | 0.07780–0.07865 ms | 0.09355–0.09493 ms | 0.82–0.84× | B1143 diagnostic. Controls L/C 1.08 / 1.48%. IQR ≤1.10%. |
| CSV invoice | Compile-to-result | 6.625–6.737 ms | 6.367–6.465 ms | 1.02–1.06× | B1143 diagnostic. Controls L/C 1.69 / 1.53%. IQR ≤0.76%. |
| Generated validator | Compilation only | 1.63857–1.66815 ms | 1.87953–1.91107 ms | 0.857–0.888× | B1213 diagnostic. Controls L/C 1.81 / 1.68%. IQR ≤3.44%. |
| Generated validator | Compile-to-result | 1.763–1.794 ms | 2.046–2.086 ms | 0.845–0.877× | B1213 diagnostic. Controls L/C 1.75 / 1.98%. IQR ≤3.16%. |
| Generated validator, successful integer parses | Compilation only | 1.415–1.436 ms | 1.770–1.781 ms | 0.795–0.811× | B1271 diagnostic. Controls L/C 1.45 / 0.61%. IQR ≤0.76%. |
| Generated validator, successful integer parses | Compile-to-result | 1.572–1.573 ms | 1.953–1.979 ms | 0.795–0.806× | B1271 diagnostic. Controls L/C 0.04 / 1.34%. IQR ≤1.14%. |
| Configuration manifest | Compilation only | 0.15576–0.15686 ms | 0.16836–0.17140 ms | 0.91–0.93× | B1130 diagnostic. Controls L/C 0.70 / 1.80%. IQR ≤0.80%. |
| Configuration manifest | Compile-to-result | 0.463–0.464 ms | 0.493–0.498 ms | 0.93–0.94× | B1130 diagnostic. Controls L/C 0.30 / 0.99%. IQR ≤2.07%. |
| Retained nonlocal siblings | Compilation only | 0.04323–0.04410 ms | 0.05508–0.05515 ms | 0.78–0.80× | B1109 diagnostic. Controls L/C 2.01 / 0.12%. IQR ≤2.17%. |
| Retained nonlocal siblings | Compile-to-result | 0.221 ms | 0.252–0.255 ms | 0.87–0.88× | B1109 diagnostic. Controls L/C 0.10 / 0.96%. IQR ≤1.02%. |
| Shipment report with named format selectors | Compilation only | 0.02448–0.02453 ms | 0.03435–0.03456 ms | 0.708–0.714× | B1233 diagnostic. Controls L/C 0.20 / 0.61%. IQR ≤3.02%. |
| Shipment report with named format selectors | Compile-to-result | 3.818–3.873 ms | 3.487–3.501 ms | 1.09–1.11× | B1233 diagnostic. Controls L/C 1.44 / 0.41%. IQR ≤1.41%. |
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
| Write default float repr and finite JSON in UTF-8 | Numeric JSON export runs 5.81–9.97% sooner and the text export 4.37–7.56% sooner, with lower CPU. Local CLR allocation falls 23.83% and 15.33%. | Accounted peaks match. Integer JSON and invoice control CPU span or touch zero. [B1279](../benchmarks/results/2026-11-21-direct-float-output/README.md). |
| Render CSV fields directly into admitted row storage | Complete restock exports run 33.77–36.29% sooner with ordinary descriptions, 19.49–23.32% sooner with escaped Unicode, and 8.72–10.08% sooner with short fields. Process CPU falls in all three. | Escaped export local CLR allocation falls 5.07% and its accounted peak rises 604 bytes. The ordinary peak rises 62 bytes. [B1277](../benchmarks/results/2026-11-21-bulk-csv-field-output/README.md). |
| Bind wide builtin calls from executable operands | Wide JSON Lines export runs 2.39–5.09% sooner warm with 3.16–5.20% lower process CPU and 6.57% less local CLR allocation. | The tolerance script allocates 32.34% less with inconclusive timing. The short explicit JSON control has mixed CPU, and the wide export's accounted peak rises by 128 bytes. [B1276](../benchmarks/results/2026-11-21-direct-builtin-operands/README.md). |
| Skip unnecessary lexical expression walks | The validator compiles 5.81–7.11% faster and reaches its result 4.93–5.03% sooner on repeat, with lower CPU. | The initial dataclass CPU increase does not recur. Warm allocation and accounted peaks match. [B1263/B1264](../benchmarks/results/2026-11-21-lexical-expression-traversals/README.md). |
| Bind expanded builtin calls from the argument span | The JSON Lines export allocates 4.90% less and uses 2.46–4.61% less process CPU. | Elapsed varies from effectively flat to 5.48% faster. The explicit-keyword control is 0.46–1.28% slower on elapsed with mixed CPU. [B1260](../benchmarks/results/2026-11-21-expanded-builtin-span/README.md). |
| Prepare annotation checks during syntax validation | Removes the separate annotation prepass. The unannotated validator compiles 1.46–4.17% faster with 2.11–3.51% lower process CPU. | Annotated elapsed changes are smaller or mixed, and CPU spans zero. Local compile allocation falls 0.43–1.23%, with matching warm allocations and accounted peaks. [B1259](../benchmarks/results/2026-11-21-shared-annotation-boundaries/README.md). |
| Pin Unicode casing and casefold in one UTF-8 pipeline | The map-based text report runs 6.24–9.78% sooner warm. Mixed-Unicode lowercasing and casefold reports run 11.73–14.04% and 7.84–15.81% sooner, respectively, with lower CPU. | The ASCII control is 0.45–3.68% slower. The casefold report allocates 9.48% less CLR storage with a 1.70% increase in accounted peak. [B1273](../benchmarks/results/2026-11-21-pinned-unicode-casing/fold/README.md). |
| Start exact positional callbacks in native slots | The map-based text report runs 6.00–10.07% sooner warm on repeat with 5.61–8.89% lower CPU. | The initial text reference spread was 16.19%. Map and ordinary JSON elapsed span zero. [B1256/B1257](../benchmarks/results/2026-11-21-native-callback-entry/README.md). |
| Enumerate typed dictionaries with the existing cursor | Sorted JSON normalization improves 1.16–4.09% warm with lower CPU and 6.19% less CLR allocation. Accounted peaks match. | The first sorted timing and both unsorted controls are inconclusive. [B1254/B1255](../benchmarks/results/2026-11-20-direct-dict-enumeration/README.md). |
| Keep native dictionary cursors in encoder frames | JSON normalization improves 2.35–7.07% warm with lower process CPU and 11.00% less CLR allocation. Accounted peaks match. | The manifest is slightly faster with no CPU gain. Validator elapsed and CPU span zero. [B1251](../benchmarks/results/2026-11-20-native-dict-item-cursor/README.md). |
| Prepare literal dictionary keys during compilation | JSON normalization improves 2.29–4.20% warm initially and 3.62–4.31% on repeat with lower process CPU. The guarded manifest uses 2,176 fewer accounted bytes. | The manifest's elapsed benefit is inconclusive. The validator control initially slows and spans zero on repeat. [B1249/B1250](../benchmarks/results/2026-11-20-compiled-literal-record-keys/README.md). |
| Direct native string-key dictionary operations | The validator improves 4.10–4.43% warm initially and 2.49–4.73% on repeat. The alternating JSON shipment improves 3.29–4.40% warm. CPU falls on both. | Named-selector timing is adverse initially and spans zero on repeat. Guarded manifest elapsed spans zero. Local accounted peaks match. [B1246/B1247](../benchmarks/results/2026-11-20-direct-native-text-dict-keys/README.md). |
| Prepare call sites during operand forwarding | Generated validator compilation is 2.70–6.27% faster with lower CPU. Local compile allocation falls 0.36–1.01% across four scripts. | Both compile-to-result ranges span zero. Forwarding warm impact remains unresolved after an exact-image repeat. [B1241/B1242](../benchmarks/results/2026-11-20-callsite-preparation/README.md). |
| Share compiled postponed annotations | Nested postponed factories run 3.63–6.19% sooner warm with lower CPU and 8.25% less CLR allocation. Accounted peaks match. | The observed and invoice controls initially lean adverse. Neither adverse result recurs in the exact-image repeat. [B1238/B1239](../benchmarks/results/2026-11-20-compiled-annotation-payload/README.md). |
| Prepare UTF-8 keys for `str.format_map` | The 3,072-record shipment runs 9.54–12.52% sooner warm with lower process CPU. CLR allocation falls 11.50% and accounted peak falls 25.93%. | The named `.format` control spans zero on the retained refinement. Compile-to-result was measured on the initial prototype. [B1236/B1237](../benchmarks/results/2026-11-20-prepared-format-map-keys/README.md). |
| Combine native body admission and drop argument rescans | Warm eager nested elapsed falls 1.08–2.76%. Postponed elapsed falls 0.37–3.98% with lower process CPU. | Async remains inconclusive. The initial invoice CPU increase does not recur with longer batches. Accounted peaks match. [B1225/B1226](../benchmarks/results/2026-11-20-prepared-entry-progress/README.md). |
| Store declaration operands directly | Inherited dataclass compilation takes 5.77–6.95% less time and compile-to-result takes 1.95–6.69% less time, with lower CPU. Compile allocation falls 0.97% in that case. | Dataclass warm elapsed spans zero. The original 1.99–3.97% manifest compile-to-result increase does not recur in the longer repeat. Eager warm controls vary. [B1220](../benchmarks/results/2026-11-20-direct-declaration-operands/README.md) · [B1221](../benchmarks/results/2026-11-20-direct-declaration-operands/confirmation/README.md). |
| Keep function defaults in final storage | Warm allocation falls 7.62–10.96% across three factory scripts. Eager rules run 0.91–2.99% sooner warm and 1.88–5.18% sooner to result. Postponed nested factories run 3.93–7.21% sooner warm. CPU supports these lanes. | Eager nested warm remains inconclusive in the repeat. Compile-only eager CPU rises 0.02–2.09% with mixed elapsed. Accounted peaks match. [B1216](../benchmarks/results/2026-11-20-default-operand-storage/README.md) · [B1217](../benchmarks/results/2026-11-20-default-operand-storage/confirmation/README.md). |
| Share executable reachability and operand layout | Generated validator compile-to-result elapsed falls 6.57–10.22% and CPU falls 7.87–10.74%. The compiler no longer builds an intermediate compacted block graph. | Validator warm elapsed rises 0.42–2.11% and CPU rises 0.85–1.44%. The two reference compiler processes differ by 10.60%. Manifest compile-to-result elapsed rises 0.11–1.06% with mixed CPU. [B1212](../benchmarks/results/2026-11-20-shared-executable-graph/README.md). |
| Create captures in inline factory slots | Nested factory warm elapsed falls 14.66–17.26% and process CPU falls 11.79–17.09%. | Allocation is effectively flat. Invoice elapsed no longer moves adversely. Eager and postponed controls vary. [B1207](../benchmarks/results/2026-11-20-inline-captured-factories/refinement/README.md). |
| Complete lexical definitions in inline slots | Eager and postponed factory elapsed falls 24.75–29.42% and nested factory elapsed falls 9.03–13.39%, with lower CPU on all three. | CSV invoice spans zero. Warm CLR allocations are effectively flat. [B1202](../benchmarks/results/2026-11-20-inline-lexical-definitions/README.md). |
| Derive class-cell demand in the lexical walk | Dataclass compilation allocates 11.10% less and takes 6.92–10.61% less elapsed with lower CPU. The other four scripts allocate 1.84–3.37% less during compilation. | Dataclass compile-to-result elapsed falls 0.09–4.99% with mixed CPU. Validator and manifest timing span zero. Warm allocations and accounted peaks match. [B1199](../benchmarks/results/2026-11-20-lexical-class-cells/README.md). |
| Reuse completed branch maps during abstract analysis | Removes the second join path and 26 net production lines. The guarded manifest's exact-image repeat takes 0.65–1.28% less compile-to-result elapsed and 1.30–2.48% less process CPU. | The first run spans 0.04–12.78% slower compile-to-result with an 11.51% candidate spread. It does not recur in the repeat. Compile-only and the unchanged warm control remain inconclusive. [B1234/B1235](../benchmarks/results/2026-11-20-abstract-join-transfer/README.md). |
| Consume validated JSON property names from the shared layout | Warm named shipment takes 2.15–7.51% less elapsed and the wide-record report takes 5.81–6.00% less, both with lower CPU. | Allocation and accounted peaks are effectively unchanged. Alternating record shapes are inconclusive. The invoice's initial slowdown does not recur in the exact-image repeat, where CPU remains mixed. [B1231/B1232](../benchmarks/results/2026-11-20-plain-json-property-names/README.md). |
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
