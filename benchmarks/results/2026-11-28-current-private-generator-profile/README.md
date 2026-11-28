# B1971 Attribute the current private generator reduction

Two warmed EventPipe captures use exact Lython `6ae6f2fa8`, Ubuntu
WSL and .NET 11 RC3 on one logical CPU. The scripts use `RunAsync`
over the same 2,048 CSV rows. One uses an ordinary helper and tuple
generator, and the other uses an explicit tuple loop. Both match
independent CPython output and file goldens. The 105 sampled batches
verify, and both workers and trace processes join.

| Managed region | Tuple generator | Explicit loop |
| --- | ---: | ---: |
| Borrowed generator lifecycle, inclusive | 36.0% | 0.0% |
| Borrowed generator instruction prefix, inclusive | 24.9% | 0.0% |
| Borrowed operand preparation, inclusive | 7.4% | 0.0% |
| Executable frame construction, inclusive | 3.5% | 0.1% |
| General instruction dispatch, nearest | 11.0% | Outside the top eight |
| Native instruction loop, nearest | Outside the top eight | 37.8% |

The shares describe resolved managed samples. Inclusive rows overlap.
The two scripts have different denominators. The paired wall and CPU
measurements remain in B1969. [B1969 comparison](../2026-11-28-inline-private-sequence-position/README.md).

The generator's hottest separate region remains its instruction prefix.
The helper and generator each retain seven slots. A useful next change
would eliminate a complete private boundary or dispatch stage while
preserving promotion, cancellation, and the admitted live state. The
sample fractions do not estimate an achievable CPU reduction.

[Attribution](attribution.json) · [Code layouts](layout.json) ·
[Image identity](identity.json) · [Run summary](summary.json) ·
[Captures and procedures](profiles.tar.xz).
