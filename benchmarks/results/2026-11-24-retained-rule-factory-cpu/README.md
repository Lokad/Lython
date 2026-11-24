# B1577 CPU attribution after scalar activation removal

This profile uses the same frozen `531499f09` image as the
[CPython checkpoint](../2026-11-24-rule-activation-cpython/README.md).
Its runtime and tests match `f17898c19`. The current captured and
defaulted scripts complete 3,968 and 4,224 invocations, respectively,
during the profiled batches.

| Nearest resolved runtime group | Captured rules | Defaulted rules |
| --- | ---: | ---: |
| Instruction dispatch | 356 | 371 |
| Entry and operand movement | 163 | 134 |
| Function and defaults construction | 140 | 75 |
| Lexical binding operations | 62 | 0 |
| Admission or retirement | 107 | 128 |
| Numeric arithmetic | 26 | 32 |
| Other resolved runtime | 338 | 472 |
| Total with a resolved runtime ancestor | 1,192 | 1,212 |

The captured script has 97 nearest samples at native definition dispatch
and 43 at callable construction. Explicit reserve samples have callable
construction as the caller 56 times and capture construction 25 times.
The defaulted script has 59 explicit reserve samples under default
storage and 54 under callable construction. Scalar return remains visible
in 95 captured and 136 defaulted inclusive samples.

Inspect the complete definition path for work that can disappear
between compiled capture/default facts and the ordinary constructor.
Retain fresh function identity and live cell aliases. These profiles
also leave substantial dispatch and entry costs visible. Sample
ancestry overlaps and the categories are heuristic, so counts give
no standalone estimate of removable CPU.

Both script goldens and all 32 image files verify. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.
Each script warmed for at least eight seconds and was sampled for
about six seconds at 199 Hz. Captured and defaulted batches have
65 and 62 unmapped leaves. Both quiet gates, worker joins, guest
fixture removal and persistent archive verification pass.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Golden checks](verification.json) · [Raw profiles](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal status](terminal-status.json).
