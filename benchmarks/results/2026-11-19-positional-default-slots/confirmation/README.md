# B1100 positional default timing confirmation

The same `3156df3b9` and `88aaeecf3` images from [B1099](../README.md) ran on
the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. This run reversed
the first worker launch order and increased each process's full-request warmup
from 8 to 12 seconds. All four combinations of duplicate process medians
are included in each range.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Computed defaults | Warm | −6.27…+7.68% | −3.86…+6.33% |
| Computed defaults | `RunAsync` warm | −3.87…+2.26% | −4.66…+4.08% |
| Ordinary rules | Warm | −3.57…−3.46% | −4.90…−2.04% |
| Captured rules | Warm | −4.30…+5.81% | −5.23…+5.53% |
| Constant defaults | Warm | −1.06…+8.31% | −1.66…+8.71% |

The ordinary-rule gain repeats with same-source spread below 0.07% on both
images. The original async slowdown does not repeat across all pairs.
Synchronous computed defaults retain a 9.57% reference spread. Captured and
constant cases have candidate spreads of 8.22% and 6.65%. Keep the private
source and both observations. R670 tests removing duplicate tuple lookup and
bounds checks at calls against the same measured reference.

All 100 measured batches matched their goldens in 310.04 seconds. Quiet gates,
workers joining, fixture cleanup and persistent archive verification passed.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Lease receipt](archive-receipt.json).
