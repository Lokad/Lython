# B1579 Confirm the defaulted rule control

The same `bd0f0ca93` and `ebfe49f02` images ran with twelve seconds
of worker warmup and reversed worker placement for these three lanes.
Input and output goldens match the first comparison. Ranges span
all pairings of the two worker medians per revision.

| Complete script | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rules | Warm | +3.95 to +7.09% | +3.04 to +4.70% | 0.42 / 2.60% |
| Defaulted rules | Compile to result | −2.64 to +3.54% | −4.62 to +4.46% | 0.86 / 5.45% |
| Annotated rules | Warm | −11.02 to −0.24% | −11.13 to −1.81% | 6.68 / 5.09% |

Keep shared definition preparation private. Defaulted warm elapsed and
CPU slow in both comparisons. Defaulted first use is inconclusive in
confirmation. Annotated controls still move, limiting the size of the
apparent improvement. The captured gains and generated-code reduction
from the first comparison remain evidence for a different factory
design. Prioritize a whole-text representation change before another
definition layout trial.

Both copied images matched the two goldens and all 32 image files
verified before timing. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 24, 2026 UTC. Boundaries match B1578.
All 84 batches pass, with quiet gates, joined workers, fixture cleanup
and persistent archive verification. Local summarization initially used
the wrong output directory. The corrected summary reads the same
verified archive.

[First comparison](../README.md) · [Timing](summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal status](terminal-status.json).
