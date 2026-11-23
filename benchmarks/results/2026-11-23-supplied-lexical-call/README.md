# B1526 share prepared call updates with checked lexical locals

Retained `be20f34fc` uses the same integer call update for an early module
load and an existing checked lexical read. A prepared instruction replaces
the previous branch on ordinary local loads. The eventual store keeps the
normal executable storage and cleanup. The frozen `c4edcc6a7` runtime matches
the retained source. The reference is `faf86299e`.

| Complete script and phase | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Fully supplied arithmetic inside a function, warm | −31.03% to −29.10% | −30.84% to −27.66% |
| Fully supplied arithmetic inside a function, compile to result | −30.05% to −28.64% | −30.40% to −28.82% |
| Fully supplied arithmetic at module scope, warm | −2.38% to +1.26% | −2.38% to +1.41% |
| Quote-heavy invoice, warm | −1.48% to +1.00% | −2.44% to 0.00% |
| Explicit-float CSV, warm | −0.92% to +4.84% | −1.00% to +4.96% |

Changes compare the four pairings of candidate and reference process medians.
Negative values mean lower cost. The explicit CSV candidate spread is
4.13%, so the size and direction of that control remain uncertain.
The invoice result spans zero against the immediate reference.
[B1524](../2026-11-23-supplied-operand-window/README.md) preserves the
earlier possible invoice and text costs against the older source.

The function script allocates 11,648 bytes rather than 77,184 in the Windows
diagnostic, removing 65,536 bytes, or 84.91%. The difference matches one
32-byte box per call. The module stays at 11,624 bytes. All five checked
scripts recover their shared budgets and keep their admitted peaks.
Only one instruction changes in each target across 13 inspected bodies.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23,
2026 UTC. Two workers per image warm for eight seconds each and contribute
seven rotated batches. Warm elapsed includes execution, applicable
RAM-backed I/O, result verification and prior-result disposal.
Time to result includes compilation and code disposal. Wider CPU includes
the full request. All 140 batches, both quiet gates, full image hashes,
worker joins, fixture cleanup and archived observations verify.

Both images match all five CPython goldens. Windows .NET 11 and Ubuntu
.NET 10 each passed 699 focused public and 649 white-box Release tests.
Four cases per platform also passed after strengthening the tests to
exercise prepared instructions in the function scope. They cover observation
through a rebound callable and cancellation while a protected loop runs.
Failure, awaited cleanup and budget recovery use the existing machinery.

[Timing](summary.json) · [Identity](identity.json) · [Tape](tape-summary.json) ·
[Test tape](test-tape-summary.json) · [Allocation](allocation-summary-six.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
