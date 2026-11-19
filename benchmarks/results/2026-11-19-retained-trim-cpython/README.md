# B1069 retained trim and closures against CPython

Measured Lython `12fabffb1` with stock CPython 3.13.16 on the quiet Azure
F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC. The
32-file Lython image is the retained B1064 runtime. Expanded forwarding,
shipment and CSV repeat their exact B1057 cases. Retained nonlocal
siblings uses the B1062 workload, including deletion/rebinding and
reference identity after the factory returns. All four verified locally
against CPython before staging.

| Workload | Input | Compilation only | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Expanded forwarding | 1,024 records / 26,112 B | 0.84× | 1.73–1.75× | 1.61–1.64× |
| Retained nonlocal siblings | 240 factories / 720 returned closures | 0.82–0.83× | 1.10–1.15× | 1.04–1.09× |
| Shipment report with format selectors | 3,072 records / 189,441 B | 0.93–0.95× | 1.17–1.22× | 1.18–1.19× |
| CSV invoice | 4,096 data records / 127,031 B | 0.95–0.96× | 1.10–1.11× | 1.08–1.11× |

Ratios divide Lython elapsed by CPython elapsed across all four pairs of
process medians. Above one means slower in Lython. Warm shipment CPython
control spread is 2.22%, compared with 10.73% in B1057. The largest warm
control spread here is 3.59% for CPython retained siblings. The previous
rule-closure cases remain in B1057. The paired B1062 and B1064 measurements
attribute the changes within Lython.

Warm includes mediated RAM-backed I/O, output goldens and previous-result
cleanup. Compilation includes compiled-owner disposal. Compile-to-result
includes compilation, execution and cleanup. Process startup, fixture reset,
request framing and final cache teardown are outside guest elapsed. Process
CPU has a wider boundary. CPython uses native file I/O.

The first launch stopped before starting workers because the collector
expected an earlier CPython-worker hash. Its logs, staged identity and
empty fixture cleanup are preserved. The corrected collector compares
against the staged digest. Both quiet gates and all 240 observations
verified in 248.82 seconds. Workers joined, fixtures were removed and the
persistent archive verified under the lease.

[Elapsed and CPU observations](summary.json) · [Raw observations](observations.tgz) ·
[Procedures and original setup failure](procedures.tar.xz) ·
[B1057](../2026-11-19-retained-capture-cpython/README.md) ·
[B1062](../2026-11-19-detached-nonlocal-cells/README.md) ·
[B1064](../2026-11-19-trim-policy-scan/README.md).
