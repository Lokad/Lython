# B1062 independently owned mutable closure cells

R629 uses admitted executable cells for complete nonlocal captures and
releases the defining frame when its namespace is unnecessary. Sibling
closures share the actual cell through rebinding, deletion and later
mutation. The measured `c43d4929c` and retained `43c9f2c38` have identical
source and tests. The reference is `d4b2088d4`.

Windows .NET 11 and WSL .NET 10 each passed 1,444 focused Release checks.
The 256 KiB test retains 240 sibling sets, checks shared mutation and
deletion through both APIs and verifies return to baseline after cleanup.
The same test exceeds the allowance on both reference paths.

| Local allocation diagnostic | Allocated bytes change | Accounted peak before → after |
| --- | ---: | ---: |
| Mutable counters | −7.37% | 1,869,400 → 820,824 B |
| Retained nonlocal siblings | −5.08% | 331,829 → 208,949 B |

These are 56.09% and 37.03% lower accounted peaks. Both complete 32-file
images and all seven cases passed CPython goldens. The corrected B1062
catalog removed the stale file checksum recorded in B1060 and reused the
identical images.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover the four combinations of candidate and reference process
medians. Negative values favor the retained runtime.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Mutable counters | Warm | −2.66…+2.04% | −3.05…+2.70% |
| Mutable counters | Compile-to-result | −1.82…+0.28% | −3.65…−1.06% |
| Retained nonlocal siblings | Warm | −5.74…−1.32% | −6.79…−1.21% |
| Retained nonlocal siblings | Compile-to-result | −4.53…−2.63% | −3.72…−3.35% |
| Ordinary rules | Warm | −2.12…+0.86% | −3.61…+2.57% |
| Captured rules | Warm | −0.35…+1.18% | −0.62…+2.53% |
| Expanded forwarding | Compile-to-result | +0.21…+1.95% | −0.64…+1.55% |
| Positional calls | Warm | −1.10…−0.01% | −2.82…+0.41% |

The retained-sibling gains hold across all pairs. Mutable-counter elapsed
remains inconclusive. Expanded forwarding shows a small adverse elapsed
result with mixed CPU. Keep it in the affected text report controls.

Both quiet gates and all 160 timed golden batches passed in 364.64 seconds.
Workers joined, fixtures were removed and the archive verified under the
lease. Warm includes mediated I/O, goldens and previous-result cleanup.
Compile-to-result adds compilation and disposal. Process CPU has a wider
measurement boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz) ·
[Original partial comparison](../2026-11-19-detached-nonlocal-cells-partial/README.md).
