# B1439 Direct operands for nested bound arithmetic

`6c56afca7` retains the measured implementation from `5b032c2cb`.
An eligible bound method called from a native prefix reads the evaluated
receiver and arguments directly. The shared checked integer evaluator
completes the return without building callee slots. Wider arithmetic,
dynamic operands and genuine suspension retain their ordinary paths.

The reference is `540dabd72` with the retained B1436 core. Only the core
differs across the two verified 32-file images. The quiet Azure F4as_v7
ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Quote-heavy CSV invoice | Warm | −4.61 to −2.12% | −4.26 to −1.31% | 1.47 / 1.12% |
| Nested-helper CSV invoice | Warm | −2.31 to +3.89% | −1.87 to +2.27% | 2.85 / 3.40% |
| Quote-heavy CSV invoice | Compile to result | −7.20 to −3.22% | −7.61 to −3.30% | 3.83 / 0.44% |
| Fully supplied helper | Warm | −1.21 to +0.46% | −1.43 to +0.29% | 0.02 / 1.67% |
| Postponed nested factories | Warm | −7.49 to −1.55% | −7.34 to −2.11% | 6.00 / 0.40% |
| Large positional control | Warm | −0.35 to +2.49% | −1.03 to +1.01% | 0.82 / 2.00% |
| Postponed nested factories | Compile to result | −1.47 to +4.04% | −2.28 to +3.03% | 0.30 / 5.28% |
| Ordinary split report | Warm | +0.14 to +2.56% | −0.33 to +2.46% | 0.27 / 2.14% |

Ranges span pairings of medians from two independent workers per source.
Each worker warms for eight seconds and supplies seven rotated batches.
Warm elapsed includes mediated RAM-backed I/O where applicable, output
verification and previous-result disposal. Compile to result also includes
compilation and compiled-owner disposal. Wider CPU includes resets and
protocol. Startup and final teardown are outside elapsed.

The quoted invoice completes 8,192 returns directly in each host mode.
Completed prefixes fall from 12,288 to 4,096 and argument-slot copies
from 28,672 to 4,096. Local allocation and admitted peaks are effectively
flat. All allowances return to baseline. The factories and supplied
helpers have matching routes. Factory warm elapsed favors the candidate
with 6.00% reference spread. First-result elapsed spans zero. The
ordinary split control takes longer, with mixed CPU and an unchanged
execution route. Carry that result alongside B1436 and B1437.

Retain the stable quoted-invoice gains in both phases. The supplied
controls span zero, including the positional controls that slowed in
B1387. Windows .NET 11 and Ubuntu .NET 10 each pass 111 focused Release
checks. Eight complete goldens match on both images. The nested-method
regression covers checked integer completion, promotion on overflow
and one user-defined multiplication. Both quiet gates and 224 timed
batches verify in 438 seconds. Workers join and the archive verifies
after fixture cleanup. Only the already archived B1437 core replica is
retired.

[Source and images](source.json) · [Timing](summary.json) ·
[Allocation and admission](local-allocation.json) · [Route counts](routes.json) ·
[Observations](observations.tgz) · [Procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json) ·
[B1387 shared entry trial](../2026-11-22-bound-return-target-slot/README.md).
