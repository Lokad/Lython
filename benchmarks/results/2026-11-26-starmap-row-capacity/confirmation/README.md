# B1794 wider starmap row timing confirmation

The same frozen source pair and verified catalog from the
[first capacity-hint block](../README.md) repeat the wide-row and
zip control scripts in `RunAsync`. The control goes first. Each
source uses two workers with seven seconds of warmup and seven
rotated batches targeting 620 ms. Timing boundaries are identical
to the earlier block. Negative changes mean less time. The ranges
span all four pairs of worker medians.

| Complete script | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Zip-to-starmap control | −0.82% to +2.36% | −0.66% to +2.65% | 2.71% / 0.48% |
| Five-item starmap list rows | +1.40% to +7.01% | +0.38% to +8.42% | 0.07% / 5.46% |

The wider candidate takes more elapsed time and CPU across all worker
pairs. The zip control spans zero in both metrics. The capacity hint
reduces allocation and retires geometrical growth, but leaves the
five-item async cost unresolved. Keep the shared construction candidate
private. Assess the remaining per-item cursor and argument publication
work before proposing that path again.


All 56 timed batches complete and both quiet checks pass. Workers
join, fixtures are removed and the persistent archive verifies. Staging
uses the unchanged image directories and repeats file and catalog
verification under the lease.

[Timing and spreads](summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
