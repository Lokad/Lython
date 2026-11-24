# B1604 Closed captured pairs against CPython

The frozen 32-file image at `d6437c26f` matches the runtime and tests
delivered at `0400f185c`. The comparison ran on November 24, 2026 UTC
with .NET 11 RC3 and stock CPython 3.13.16 on a quiet Azure F4as_v7
running Ubuntu 24.04.4. Two workers per engine and lane each ran seven
rotated batches after eight seconds of warmup. Ratios span the four
pairings of process medians and divide Lython time by CPython time.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Generated rules with captured locals | Warm | 0.93583–0.98667 ms | 0.76178–0.77355 ms | 1.210–1.295× | 5.43 / 1.55% |
| Postponed nested annotated rules | Warm | 0.60049–0.60089 ms | 0.53475–0.55209 ms | 1.088–1.124× | 0.07 / 3.24% |
| Generated rules with captured locals | Compilation only | 0.02672–0.02691 ms | 0.04689–0.04742 ms | 0.563–0.574× | 0.72 / 1.12% |
| Generated rules with captured locals | Compile to result | 0.95557–0.96751 ms | 0.82570–0.82767 ms | 1.155–1.172× | 1.25 / 0.24% |

Both scripts construct and apply 2,048 groups, capture stdout and use
no guest file I/O. Warm execution reuses compiled code in a warmed
process and includes result verification and disposal of the previous
result. Compilation includes code-owner disposal. Compile to result
includes compilation, one execution and cleanup. The wider CPU clock
includes per-request reset and framing. All lanes exclude process launch
and final teardown.

Both goldens and 112 timed batches passed verification. Both quiet gates,
worker joins, guest fixture cleanup and persistent archive verification
passed. The warm captured worker spread leaves the size of that gap less
precise than the compile-to-result gap. B1603 records a distinct
within-Lython postponed regression against the previous runtime.

[Timings and CPU](summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json) ·
[B1603 within Lython](../2026-11-24-closed-capture-pair/seeded/README.md) ·
[Earlier CPython checkpoint](../2026-11-24-private-calls-cpython/README.md).
