# B1660 Defaulted-rule phase split

The same frozen [B1659](../2026-11-24-direct-operand-return/README.md) images
isolate the direct operand-return patch on `694c1645b`. Both images verify
the defaulted-rule and configuration-manifest goldens against CPython
3.13.2. The comparison excludes the separately qualified capture rollback
fix. The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC.

Compilation includes disposal of the compiled script. Warm runs reuse the
script and include mediated I/O, output verification and previous-result
disposal. Compile to result includes both phases. The wider process CPU
boundary includes fixture reset and protocol framing. All boundaries
exclude process startup. Each of the four workers warms for at least five
seconds and supplies seven rotated batches with a 0.75-second batch target.
Ranges use all four pairings of process medians.

| Complete script | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rules | Compile | −1.64 to +0.35% | −1.92 to +1.47% | 0.62 / 1.39% |
| Defaulted rules | Warm | −1.95 to +1.66% | −2.53 to +1.96% | 1.57 / 2.08% |
| Configuration manifest | Compile | +0.62 to +1.55% | +0.67 to +0.97% | 0.01 / 0.92% |
| Defaulted rules | Compile to result | −1.51 to +8.32% | −2.26 to +8.92% | 2.30 / 7.51% |

The separate phases give mixed signs and leave the earlier defaulted-rule
slowdown unattributed. The combined lane varies more than the earlier
1.39–1.89% adverse CPU range. The manifest's small compile-only increase
is visible in this block. Reassess these cases when a change materially
affects their path. The earlier pair-ownership comparison uses different
images and retains its separate adverse result.

Both quiet gates, all 112 batches, worker joins, fixture cleanup and
verified archival pass.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
