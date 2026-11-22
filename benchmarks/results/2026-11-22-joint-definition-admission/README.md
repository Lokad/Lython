# B1331 Joint callable and closure capture admission

Private `cc2796536` admits callable and capture storage together for
eligible lexical definitions. The earlier eager annotation preflight
still owns its allocation order. A later abort refunds the unpublished
callable share and keeps live captures funded. The candidate displaces
a reserve and commit in the eligible path.

Compared `318bf0e0a` and `cc2796536` on November 22, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Two processes
per revision warmed for at least twelve seconds and supplied seven
rotated batches each. Ranges cover all four process-median pairings.
Warm includes output verification and prior-result disposal. Time to
result includes compilation, execution and owner disposal. Process
CPU covers the full request. Startup and final teardown are separate.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Fully supplied helper | Warm | −2.05 to −0.11% | −1.28 to +0.97% | 0.79 / 1.18% |
| Dynamic postponed factory | Warm | −3.24 to +3.82% | −2.55 to +3.35% | 3.28 / 3.89% |
| Mixed literal defaults | Warm | −2.11 to −0.74% | −2.46 to −0.68% | 0.95 / 0.44% |
| Eager factory | Warm | −3.86 to +1.50% | −4.16 to +2.61% | 2.72 / 2.78% |
| Default-observing factory | Warm | +1.37 to +1.47% | +0.57 to +2.56% | 0.09 / 0.01% |
| Mixed literal defaults | Time to result | +7.15 to +12.38% | +7.51 to +13.77% | 1.83 / 3.00% |

Keep the design private. The mixed warm gain is small relative to the
adverse time-to-result result. The observer also slows with tight
controls. The desktop helper regression does not recur. The comparison
does not separate compilation and first-use execution within the
adverse lane. Another design should displace storage or operand
transport and avoid adding checks to the ordinary definition path.
The released code keeps its existing admission boundaries.

Windows .NET 11 and Ubuntu .NET 10 each pass 1,615 white-box and 3,620
public focused Release checks. Seven CPython goldens match on both
32-file images. All admitted peaks match and each allowance returns
to baseline. CLR allocation is effectively flat. The separate desktop
diagnostic retains the larger warm gain and adverse helper and observer
results. All 168 timed batches pass, workers join and the persistent
archive verifies under the quiet lease after fixture cleanup.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Desktop observations](local-timing.json) · [Raw observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz).
