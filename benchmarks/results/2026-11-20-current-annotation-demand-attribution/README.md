# B1219 Current annotation demand and compiler attribution

Profiled `a8a7c6ba2` with the source-matched `9741634b1` image on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC.
The worker compiles and disposes one code owner per invocation. After
an eight-second warm-up, each capture samples roughly five seconds of
all-thread CPU. Guest execution is outside the sampled phase. The
profiles also include worker framing and other surrounding work.

| Samples | Eager rules | Inherited dataclasses | Configuration manifest |
| --- | ---: | ---: | ---: |
| Process sample count | 1,001 | 1,031 | 1,020 |
| Resolved compiler ancestry | 987 | 1,018 | 1,003 |
| Parser | 154 | 185 | 163 |
| Lexical scope preparation | 151 | 145 | 132 |
| Annotation flow | 39 | 30 | 0 |
| Annotation demand discovery | 8 | 1 | 0 |
| Abstract interpretation | 163 | 152 | 296 |
| Container effect analysis | 12 | 14 | 24 |
| Executable construction | 289 | 368 | 260 |
| Operand layout and linearization | 58 | 51 | 67 |

Counts use resolved call ancestry and can overlap. The compiler's
annotation prepass only stores bounds needed when exiting lexical
scopes, but also indexes control-flow suites whose complete exit facts
are used. The prepass itself appears on eight eager stacks, one dataclass
stack and none on the unannotated manifest. That scale does not support
widening the shared lexical worklist or adding cross-pass bookkeeping
to move those bounds. Continue examining the larger abstract binding
and executable construction stages before selecting a redesign.
These profiles establish neither a removable CPU fraction nor speedup.

All 32 worker files and three reference workload goldens verified
before staging. Each worker verified its workload again before and
after sampling. Both machine checks passed. All workers joined and
the persistent archive verified before guest fixtures were removed.

[Attribution](attribution.json) · [Raw profiles](observations.tgz) ·
[Source and procedures](procedures.tar.xz).
