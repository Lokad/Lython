# B1716 Compile executable lambda headers directly

The executable compiler emits lambda defaults directly from syntax. One
compiled definition supplies code and closure mode. Lowered consumers
prepare a default expression tree only where they evaluate the header.
Each of the two generated scripts contains 48 lambdas with defaults.
Compiling either script allocates 28,288 B less, a reduction of 1.9%.
Retained compilation allowance, runtime allocation on the generated cases,
and peak execution admission across all four scripts match.

The reference is `0a8952725`. Both frozen Release images share all files
except the core library. Four complete script goldens passed for each image
before staging. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3
on November 25, 2026 UTC. Two workers per image each warmed for five full
request seconds and contributed seven rotated batches per phase. Ranges
span all four pairs of worker medians. Negative changes mean less time.

Compilation includes disposal of the compiled owner. Compile-to-result
includes execution, verification and cleanup in a running process. Warm
shipment includes mediated RAM-backed I/O, verification and result cleanup.
Wider CPU includes fixture reset and request framing. Startup and final
process teardown sit outside these clocks.

| Workload and phase | Elapsed change | Wider CPU change | Elapsed spread reference / candidate |
| --- | ---: | ---: | ---: |
| Ordinary definitions, compilation | −0.70 to +1.10% | −1.46 to −0.04% | 0.12 / 1.69% |
| Live-alias definitions, compilation | −2.29 to −0.95% | −4.16 to −1.48% | 0.85 / 0.52% |
| Ordinary definitions, compile-to-result | −2.31 to +0.28% | −2.22 to +1.54% | 1.41 / 1.22% |
| Live-alias definitions, compile-to-result | −1.90 to −0.20% | −1.77 to +1.06% | 1.38 / 0.35% |
| Eager annotations, compilation control | −1.25 to +1.63% | −0.78 to +2.32% | 1.57 / 1.33% |
| Shipment, warm async control | −2.07 to −0.17% | −1.63 to +0.55% | 0.30 / 1.63% |

Retain the removed lowering stage and the allocation improvement.
Live-alias compilation is faster in this block. Its compile-to-result CPU
and ordinary-definition elapsed remain inconclusive. The unchanged warm
shipment elapsed also moves favorably. Its allocation diagnostic varies by
174 B on about 1.31 MB, at the same admitted peak.

Windows .NET 11 and Ubuntu .NET 10 each passed 3,697 focused Release
checks. A CPython 3.13.2 probe matched default order, nested closures,
class-scoped defaults in a generator lambda and lazy alias identity in
both hosting modes. All 168 timed batches and both quiet gates passed.
Workers joined, guest fixtures were removed and the persistent archive
verified under the lease.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Compilation allocation](compile-allocation-summary.json) ·
[Runtime allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures and patch](procedures.tar.xz).
