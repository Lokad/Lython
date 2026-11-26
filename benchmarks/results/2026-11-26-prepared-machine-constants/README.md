# B1770 Prepared machine constants

The prototype prepares exact signed integer values in the existing
compiled owner. Ordinary executable reads and closed numeric sequences
use that metadata instead of classifying the boxed compiler constants
again. Python-visible reads and general arithmetic keep the original
objects. The [source inventory](../2026-11-26-post-deferral-factory-cpu/README.md)
counts thousands of repeated classifications on each affected script.
The file-fed CSV map covers the unchanged control with zero successful
machine-constant reads.

| Workload | Extra compile CLR bytes | Extra retained compiler admission | Execution peak admission change |
| --- | ---: | ---: | ---: |
| Ordinary generated definitions | 4,640 B | 3,776 B | 0 B |
| Escaping defaulted rules | 320 B | 240 B | 0 B |
| Unicode text report | 344 B | 272 B | 0 B |
| File-fed CSV map control | 16 B | 0 B | 0 B |

Reference and candidate have identical measured execution allocation on
all three affected workloads. The control differs by 166 B. Compilation
measurements include compiled-owner cleanup. Every allowance returns to
baseline. Both 32-file frozen images match four independent CPython output
and file goldens. Windows .NET 11 passes 441 white-box and 1,491 public
focused Release checks, including integer promotion and compiler admission.

| Workload and lane | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Ordinary definitions, warm | −37.46 to +6.10% | −20.81 to −2.05% |
| Escaping rules, compile-to-result | −16.45 to +68.67% | −15.26 to +14.52% |
| File-fed CSV map control, warm | −23.84 to −15.65% | −31.94 to +15.48% |
| Unicode text report, warm | −41.01 to −8.03% | −16.24 to +42.15% |
| Ordinary definitions, compile-to-result | −31.44 to +312.50% | −17.50 to +237.04% |
| Escaping rules, warm | −17.95 to +11.71% | +0.98 to +13.19% |
| Unicode text report, compile-to-result | −2.74 to +23.05% | −10.81 to +10.00% |

The local pinned WSL comparison on November 26, 2026 uses df71fc295 and
the archived patch on .NET 11 RC3. It has two workers per source, three
seconds of warmup and seven rotated batches per lane. Elapsed includes
verification, cleanup and applicable RAM-backed I/O. Compile-to-result
also includes compilation and compiled-owner disposal. Wider process
CPU includes framing and fixture reset. The ranges span all pairings of
worker medians. Negative values mean less time.

Timing is inconclusive. The unchanged control differs by 48.47% between
reference CPU workers, and one ordinary compile-to-result candidate worker
takes more than three times the elapsed of its same-source peer. Keep
the warm escaping-rule CPU increase and extreme compile-to-result result
with the original observations. The measured compiled-storage cost and
unchanged outputs are established independently of timing. All 196
batches verified and workers joined. A separate quiet confirmation uses
the same frozen pair and supports keeping the change.
[Quiet confirmation](confirmation/README.md).

[Timing](summary.json) · [Compile allocation](compile-allocation-summary.json) ·
[Execution allocation](allocation-summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal receipt](archive-receipt.json) ·
[Patch and procedures](procedures.tar.xz).
