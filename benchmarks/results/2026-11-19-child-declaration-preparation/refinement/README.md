# B1098 ordered declaration preparation

R668 preserves the original order of named definitions, lambdas and generator
expressions while preparing lexical flags and capture locations. It removes
the separate function inventory and later child-capture scan. The existing
local-slot map identifies the parent. The actual capture arrays remain
compilation-owned and admitted before allocation.

The exact source pair is `482b45a89` versus `3156df3b9`. The candidate source
and tests match the retained `48a4fca4a`. The [original B1097 comparison](../README.md)
preserves the `locals()` ordering regression and adverse annotated run.
The revision matches the CPython ordering probe and passes 1,074 focused
Release checks on each of Windows .NET 11 and Ubuntu .NET 10. Both complete
worker images matched six goldens. Local allocation falls 3.41–4.02% for
the three rule compilations. Each compiled owner returns its allowance.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026. Ranges cover all four combinations of duplicate process medians.
Compilation includes compiled-owner disposal. Compile-to-result adds
execution, mediated I/O, goldens and result cleanup. Warm uses an already
compiled script. Process CPU has a wider boundary.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rules | Compilation | −6.01…−3.00% | −6.26…−1.64% |
| Annotated rules | Compilation | −5.32…−3.65% | −6.03…−4.71% |
| Captured rules | Compilation | −5.28…−2.92% | −4.76…−3.60% |
| Generated validator | Compilation | +0.77…+5.56% | −0.52…+4.11% |
| Annotated rules | Compile-to-result | −4.90…+0.34% | −3.41…+2.36% |
| Captured rules | Compile-to-result | −3.54…−1.77% | −3.84…−0.26% |
| Shipment report | Warm | −0.24…+2.18% | 0.00…+1.17% |

Retain the ownership and compiler simplification with the rule gains. The
initial annotated compile-to-result slowdown did not reproduce across all
pairs. The validator processes differ by 4.61% on the candidate and use
more CPU in three pairs. Carry that control and shipment warm CPU into the
next combined checkpoint. All 140 verified batches, both quiet gates, worker
joins, fixture cleanup and persistent archive checks completed in 322.76
seconds.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz) · [Lease receipt](archive-receipt.json).
