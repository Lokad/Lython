# B1022 retained function and split changes against CPython

Measured Lython `6ce82ba48` with core `589d6b7b` against stock
CPython 3.13.16 on 2026-11-19 UTC. The quiet Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3. The three scripts and fixtures
match B1016 exactly. The new source includes direct definition
completion and fixed split unpacking. Full platform qualification
for this source was in progress when it was measured.

| Workload | Compilation only | Warm | Compile-to-result |
| --- | ---: | ---: | ---: |
| Expanded forwarding | 0.82–0.83× | 1.86–1.87× | 1.74× |
| Unannotated rule closures | 0.78–0.79× | 4.33–4.46× | 4.14–4.28× |
| Annotated rule closures | 0.82× | 5.33–5.60× | 5.10–5.27× |

Ratios are Lython elapsed divided by CPython elapsed, across both
process medians per engine. Warm same-source Lython control spreads
are 0.49% for forwarding, 2.69% for unannotated rules and 3.77% for
annotated rules. Annotated warm reaches 2.60% IQR. Treat the ratios
as a diagnostic snapshot. Attribute production changes with the
paired Lython comparisons. [B1020](../2026-11-19-direct-function-completion/README.md) · [B1021](../2026-11-19-final-split-operands/README.md).

The same processes alternated phases for five measured rounds.
The full catalog and target images verified, both quiet gates passed
and all 180 timed responses matched their goldens. The run took
187.91 seconds. Every worker joined and the archive was verified
before fixture cleanup. Warm includes mediated RAM-backed I/O,
output checks and previous-result cleanup. Compile-only includes
compiled-owner disposal. Compile-to-result includes compilation,
execution and its cleanup. Startup, fixture reset, protocol and
final cache teardown are outside guest elapsed. Wider process CPU
is available in the raw evidence.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Earlier source](../2026-11-18-annotation-and-forwarding-cpython/README.md)
