# B1766 Whole-script comparison for exact boolean increments

Retain the exact augmented-integer addition route. The shipment callback
report uses 3.91–11.33% less process CPU in the first async block and
5.75–8.05% less in the repeat. Warm sync elapsed falls in both blocks.
The repeat sync CPU range spans zero. The typed CSV meter spans zero on
both clocks, and the text control reverses direction between blocks.
Peak admission matches and the local allocation diagnostic changes by at
most 940 B per execution. [Routes and allocation](../README.md).

| Workload and lane | First elapsed | First process CPU | Repeat elapsed | Repeat process CPU |
| --- | ---: | ---: | ---: | ---: |
| Shipment callback report, warm RunAsync | −12.38 to −4.20% | −11.33 to −3.91% | −8.32 to −5.10% | −8.05 to −5.75% |
| Shipment callback report, warm sync | −11.69 to −6.37% | −11.43 to −7.17% | −5.84 to −0.18% | −4.92 to +0.61% |
| Typed Unicode CSV meter, warm sync | −3.57 to +3.09% | −2.58 to +3.31% | −0.90 to +2.51% | −0.82 to +3.54% |
| Unchanged Unicode text, warm sync | +1.32 to +5.34% | −0.33 to +5.63% | −11.29 to −3.16% | −9.96 to −1.67% |

The two November 26, 2026 blocks use the same 32-file frozen pair
built from 7f7c4ed59 and the archived patch. The Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3. Each lane uses two fresh workers per
source and seven rotated batches after five seconds of warmup per
worker. The first block targets 0.44 seconds per batch. The repeat
targets 0.70 seconds, starts with text and switches the initial
worker ordering. Changes span all four pairings of worker medians.
Negative values mean less time. Warm elapsed includes mediated
RAM-backed I/O, output and file verification and result disposal.
Wider process CPU includes reset and framing. Compilation and startup
sit outside these clocks.

The repeat has 0.36% reference and 2.12% candidate worker spread
in async shipment CPU. Reference sync shipment CPU spread reaches
5.46%, and the text control reaches 5.53%. The reverse text movement
leaves its underlying cause unresolved. The first raw block's
descriptive warmup string says seven seconds while its actual
warmupSeconds setting and collection loop use five.

Both blocks pass the independent CPython output and file checks,
all 112 timed batches, both quiet gates, worker joins, fixture
removal and persistent archive verification. Focused Release checks
pass on Windows .NET 11 and Ubuntu .NET 10, including a CPython golden
for exact boolean increments with retained identity, overflow and
custom dispatch.

Delivery for df71fc295 passed Windows and Ubuntu Release on .NET 11
and .NET 10, original stress and installed-package consumers.
[Delivery](https://github.com/Lokad/Lython/actions/runs/45841883264).

[First timing](initial-summary.json) · [First observations](initial-observations.tar.xz) ·
[First terminal status](initial-terminal-status.json) · [Repeat timing](repeat-summary.json) ·
[Repeat observations](repeat-observations.tar.xz) · [Repeat terminal status](repeat-terminal-status.json) ·
[Procedures](procedures.tar.xz).
