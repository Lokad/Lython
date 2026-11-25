# B1682 Carry default ownership through callable construction

The refinement keeps the default slot and its ownership tag together from
definition storage into the callable. A single retirement routine handles
unpublished backing. A caller or metadata observer publishes the canonical
integer in the same slot. The two separate inline-default flags are removed.
Both admission points, the failure order, and live replacement semantics
are preserved. An observed keyword integer stays funded after replacement.

The same `a39d00228` reference and seven CPython 3.13.2 goldens are used as in
[B1681](../2026-11-25-keyword-default-operands/README.md). All 216 executable
bodies and 2,075 instructions, spans and retained compiler admission match.
Windows passes 493 focused public checks per Release target. Ubuntu .NET 11
passes 493 public and 37 white-box checks, including alias publication,
funded replacement, cancellation and refused admission.

| Complete script | Reference allocation | Refined allocation | Admitted peak in both |
| --- | ---: | ---: | ---: |
| Generated ordinary definitions | 249,464 B | 200,344 B | 971,730 B |
| Generated default-free definitions | 197,968 B | 197,968 B | 249,652 B |
| Defaulted rule pairs | 737,992 B | 737,992 B | 2,786,655 B |
| Eager annotated rules | 325,016 B | 259,480 B | 2,444,081 B |
| Postponed annotated rules | 570,896 B | 570,896 B | 2,902,945 B |
| Quote-heavy invoice | 3,281,470 B | 3,281,420 B | 9,040,266 B |
| Shipment callback report | 1,315,464 B | 1,315,464 B | 2,817,644 B |

Ordinary and eager definitions allocate 19.69% and 20.16% less. Removing the
flags recovers the increases on the default-free, defaulted and postponed
controls. Every allowance returns to baseline after owned cleanup.

## First quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Four workers warmed for five seconds and ran seven rotated batches with a
0.87-second target. Ranges span four worker-median pairings. Warm execution
includes applicable mediated RAM I/O, output checks and previous-result
disposal. Wider CPU includes fixture reset and request framing. Compilation
and startup sit outside these clocks.

| Workload | Warm elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Generated ordinary definitions | −6.62 to −3.99% | −6.60 to −3.62% | 1.36 / 1.44% |
| Generated default-free definitions | −6.32 to −4.84% | −6.30 to −3.86% | 1.23 / 0.35% |
| Defaulted rule pairs | +0.66 to +14.66% | +1.53 to +14.44% | 2.05 / 11.62% |
| Eager annotated rules | −10.25 to +4.18% | −10.20 to +2.97% | 7.72 / 7.76% |
| Quote-heavy invoice | +0.17 to +0.36% | −0.41 to +0.08% | 0.14 / 0.04% |

All 140 batches, both quiet gates, worker joins and archival verification
pass. Ordinary and default-free definitions spend less CPU. The defaulted
rule increase and eager uncertainty warrant a longer rotation on the same
binaries before selecting the implementation.

## Longer confirmation and retention

The same verified binaries use eight seconds of warm-up per worker and a
one-second batch target with a different workload order. All 84 batches,
both quiet gates, verified joins and archive checks pass.

| Workload | Warm elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Eager annotated rules | −6.29 to −3.93% | −5.87 to −3.97% | 2.19 / 0.32% |
| Generated ordinary definitions | −7.83 to −3.42% | −7.80 to −3.50% | 3.35 / 1.39% |
| Defaulted rule pairs | −1.86 to +5.28% | −2.05 to +6.06% | 4.14 / 3.01% |

Retain the shared ownership representation for the removed flags and
eager boxing, the allocation savings, and the ordinary and eager warm gains.
Defaulted CPU crosses zero in the confirmation. Keep the initial adverse
measurement and revisit it with the next affected call-entry change.
Postponed CPU and compilation-to-result timing remain open.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocations](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal status](quiet-terminal-status.json) ·
[Procedures and source](procedures.tar.xz) · [Confirmation timings](confirmation/quiet-summary.json) ·
[Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation terminal status](confirmation/quiet-terminal-status.json) ·
[Confirmation procedures](confirmation/procedures.tar.xz).
