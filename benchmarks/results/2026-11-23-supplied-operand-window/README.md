# B1524 keep supplied call inputs and the accumulator in their slots

Retained `faf86299e` reuses the physical operand window for checked argument
arithmetic through a closed integer return and adjacent local addition.
The frozen candidate `70cd1179e` matches the retained runtime and test source.
The reference is `4b2700e5a`. Only the accumulator read in the module acquires
a marker across the 13 inspected bodies. Both images match five CPython
goldens. Observed values continue through the existing canonical object path.

| Complete script and phase | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Fully supplied arithmetic, warm | −38.80% to −36.35% | −40.12% to −37.21% |
| Fully supplied arithmetic, compile to result | −34.96% to −34.79% | −36.48% to −34.04% |
| Fully supplied arithmetic inside a function, warm | −1.94% to +2.55% | +0.43% to +2.55% |
| Quote-heavy invoice, warm | +0.33% to +3.34% | −1.10% to +3.85% |
| Short nonlocal word report, compile to result | −6.29% to +10.53% | −6.34% to +9.38% |
| Explicit-float CSV, warm | −0.65% to +6.24% | −0.82% to +5.13% |

Changes compare the four pairings of two candidate and two reference worker
medians. Negative values mean lower cost. The word-report reference spread
is 11.85% and the candidate spread is 5.46%. The explicit CSV candidate
spread is 5.25%. The quote-heavy invoice's possible low-single-digit cost
and the function-scoped case's small CPU increase remain follow-up checks.

Windows diagnostic allocation for the supplied module falls from 142,664
to 11,624 bytes per invocation, a 91.85% reduction. The 131,040-byte
difference is consistent with eliminating 4,095 32-byte boxes across
2,048 iterations, with the initial accumulator already a boxed constant.
The admitted peak stays 1,255 bytes. The function-scoped case keeps its
existing checked lexical read and allocates 77,184 bytes in both images.
Peak admission and budget recovery agree across the five cases.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23,
2026 UTC. Two workers per image warmed for eight seconds each and contributed
seven rotated batches. Warm elapsed includes execution, applicable RAM-backed
host I/O, result verification and prior-result disposal. Time to result adds
compilation and code disposal. Wider CPU includes whole-request work. All 168
batches, both quiet gates, each full image, worker joins, fixture cleanup and
the persistent archive verify.

Focused Release passed 699 public tests on Windows .NET 11 and Ubuntu .NET
10 and 649 white-box tests on Windows .NET 11. Both host APIs exercise wide
results, zero division, a callable observing the original argument, and a
reentrant numeric protocol that suspends for a host read. Its single cleanup
and shared-budget recovery are asserted.

[Timing](summary.json) · [Identity](identity.json) · [Tape changes](tape-summary.json) ·
[Allocation](allocation-summary-six.json) · [Observations](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
