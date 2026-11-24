# B1589 Read captured locals at arithmetic and conditional operands

Private `983853f0` forwards captured-local reads to the existing arithmetic
and conditional operand readers. The exact `af717101d` reference has the
runtime source of `3ef18ef8d`. The captured factory loses three instructions
in `make_rules` (20 to 17), and a second fixture removes three loads across
arithmetic and a condition while preserving the left evaluation snapshot
through an effectful right operand. Slot capacities stay unchanged. None of
the four timed controls changes compiler instruction count or slot capacity.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Both 32-file images differ only in the core DLL and verify five
CPython 3.13.16 goldens. Each image has two workers with eight-second warmups
and seven rotated batches. Ranges are across pairings of worker medians.
Warm elapsed includes mediated RAM-backed I/O, output verification and
previous-result disposal. Wider CPU includes reset and protocol. Compilation,
startup and final teardown sit outside these clocks.

| Warm script | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Captured rules | −6.40 to +0.68% | −7.16 to +1.86% | 0.65 / 6.88% |
| Defaulted rules control | −0.07 to +5.11% | −0.57 to +5.16% | 4.31 / 0.84% |
| Annotated rules control | −12.17 to −2.56% | −11.07 to −2.20% | 9.36 / 1.45% |
| Short text report control | +0.91 to +8.22% | +0.53 to +8.13% | 4.27 / 2.85% |
| Quoted CSV invoice control | −3.02 to +3.03% | −2.61 to +1.28% | 2.68 / 3.47% |

Keep this version private. The captured result is inconclusive, and the text
control worsens. The annotated control also moves favorably with wide reference
spread. These observations leave attribution uncertain. The native scalar
operand reader changed for all scripts, so a compiler-only refinement can
isolate the eliminated loads. Local allocation is effectively flat, admitted
peaks match and the allowance returns after each run.

Windows and Ubuntu each pass 213 public and 63 white-box focused Release
checks. The added cases preserve unbound-local names and source spans, the
capture snapshot across a mutating call and the identity of a retained scalar.
Both quiet gates pass. All 140 batches verify, workers join, fixtures retire
and the persistent archive validates. Staging reused the exact B1584 catalog
and retired only an independently verified archived B1489 binary replica.

[Timing and spread](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identities](identity.json) · [Reference core](../2026-11-24-eager-json-owner/candidate-core.dll.gz) ·
[Observations](observations.tar.xz) · [Procedures and compiled shapes](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
