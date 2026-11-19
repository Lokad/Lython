# B1112 direct forwarding into final defaults

Refinement `8ffd40dc0` uses the existing instruction result consumer to fill
the private defaults when a straight-line expression completes. It retains
the ordinary append at control-flow and pending-cleanup boundaries. The
positional loan and deferred tuple construction remain removed. The measured
source and tests match retained commit `e1636b271`.

| Workload | Boundary | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Ordinary rules | Warm | −2.53…−1.61% | −2.87…−1.92% | 0.48 / 0.47% |
| Annotated rules | Warm | −3.47…+1.20% | −1.96…−0.34% | 1.89 / 2.89% |
| Ordinary rules | Compile-to-result | −2.99…−1.68% | −3.42…−1.47% | 0.15 / 1.20% |
| Computed defaults | Warm `RunAsync` | +0.14…+1.16% | −1.45…+1.40% | 0.10 / 0.92% |
| Named forwarding | Warm | −1.64…+1.32% | −2.52…+1.61% | 1.56 / 1.42% |

The unchanged `3e67fd010` reference and the refined candidate ran on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026. Ranges cover all four combinations of duplicate process medians.
Warm includes mediated RAM-backed I/O, goldens and prior-result cleanup.
Compile-to-result adds compilation and disposal of the compiled owner.
Process CPU covers the wider request.

Local warm allocation falls 8.75% on ordinary rules and 6.94% on annotated
rules. Accounted peaks match or fall by 48 bytes on those two cases. The
constant-default control retains the 40-byte peak increase. Every
allowance returned to baseline.

Keep the refinement for the removed construction stage and the ordinary
rule gains. The original B1111 slowdown remains in the evidence.
Annotated warm elapsed and named forwarding remain inconclusive.
Asynchronous computed defaults remain a control owing to their longer
elapsed with mixed CPU. [Initial comparison](../README.md).

Windows .NET 11 and Ubuntu .NET 10 each passed 171 focused Release checks.
Both complete 32-file images matched six CPython goldens. Eight probes
matched CPython through both APIs. All 100 timed batches verified in
269.69 seconds and both quiet gates passed. Workers joined, guest
fixtures were removed and the archive was verified under the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
