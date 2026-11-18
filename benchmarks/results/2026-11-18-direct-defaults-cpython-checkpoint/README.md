# B1002 representative 0.11.0 scripts against CPython

Measured `53df08565` / frozen core `6c4cb50a` on 2026-11-18 UTC against
stock CPython 3.13.16 with PGO/LTO, the GIL and no JIT. The four-vCPU Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11.0.0-rc.3.26522.148. Two processes per
engine and case alternated compile, warm and compile-to-result. Each engine's
range uses five measured rounds per process and phase. The collection took
315.57 seconds. Both quiet checks passed, all stdout and file goldens
matched and all workers joined before fixture removal and archive verification.

| Workload | Input | Lython / CPython compile | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Configuration edit | 181 B / 16 paths | 1.05–1.07× | 1.23–1.27× | 1.06–1.09× |
| Expanded forwarding report | 1,024 records / 26,112 B | 0.87–0.88× | 2.37–2.45× | 2.19–2.26× |
| CSV invoice | 4,096 data records / 127,031 B | 1.03–1.05× | 1.08–1.11× | 1.07–1.09× |
| Helper text normalization | 52,224 B / 4,096 lines | 1.02–1.03× | 1.43–1.45× | 1.31–1.39× |
| Rule closures | 2,048 inputs / 4,096 definitions | 0.77–0.78× | 5.18–5.27× | 5.04–5.07× |

Ranges cover all combinations of the two elapsed medians per engine. The
forwarding warm controls spread 2.07% for Lython and 1.24% for CPython. Rule
closures spread 1.08% and 0.58%, respectively. Both have larger execution
gaps than the other scripts, despite faster compilation. The helper text
warm IQR reaches 5.75% and its compile-to-result control spreads reach 3.06%.
These are diagnostic engine comparisons, with source-pair changes measured
in their own experiments.

Warm elapsed includes execution, mediated RAM-backed guest file I/O, golden
checks and previous-result cleanup. Compile-to-result adds compilation and
compiled-owner disposal. Compilation alone includes compiled-owner disposal.
Fixture reset, process startup, protocol and final cache teardown are outside
guest elapsed. The wider process CPU samples are kept separately. CPython runs
native file APIs, while Lython uses the comparison host's mediated equivalents.

The first local launcher-generation attempt had a Python syntax error. The
corrected launcher verified the complete frozen image, retired only exact
archived core replicas and performed the recorded collection. The initial
script and successful procedures are both preserved.

[Times, CPU and control spreads](summary.json) · [Observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Terminal state](terminal-status.json)
