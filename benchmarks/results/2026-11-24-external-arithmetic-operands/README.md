# B1574 Share external operand classification in arithmetic

The scalar-return change grew the local JIT's general integer evaluator
from 877 to 1,252 bytes. It inlined the capture carrier dispatch at both
operand reads. The follow-up keeps that dispatch in the existing capture
reader and classifies constants, globals and captures relative to the
constant table. The local evaluator measures 1,007 bytes. These Windows
x64 .NET 11 RC3 FullOpts listings identify the code that disappeared.
Elapsed and CPU are measured separately on the quiet VM.

| Complete script | Warm elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Captured rules | −1.31 to +2.69% | −0.30 to +1.67% | 2.94 / 1.08% |
| Defaulted rules | −4.91 to −1.55% | −5.28 to −2.65% | 0.48 / 3.04% |
| Short nonlocal report | −6.59 to −1.23% | −6.79 to +0.53% | 1.96 / 3.71% |
| Quoted invoice | −0.56 to +0.52% | −1.96 to −0.61% | 0.73 / 0.36% |

Retained in `f17898c19`. Defaulted elapsed and CPU fall. The short
report takes less warm elapsed with mixed CPU. Captured timing and
quoted elapsed span zero. Keep both original B1572 control regressions
in the record. These separate pairs leave the amount of cumulative
recovery uncertain. Admission peaks match and all four allowances
return to baseline. Only the quoted allocation diagnostic differs,
by 42 B per invocation.

Windows and Ubuntu each pass 94 focused .NET 11 Release checks,
including capture rebinding, exact/wide arithmetic, cancellation and
admission. All four CPython 3.13.16 goldens pass on both 32-file images.
Only the core differs between frozen `768cd861d` and `531499f09`,
matching production runtimes `df89d5b6b` and `f17898c19`.
The dedicated Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 24, 2026 UTC. Two workers per source warmed for eight
seconds and alternated seven samples each. Elapsed includes
applicable mediated RAM-file I/O, golden checks and prior-result
disposal. Wider CPU includes request handling and reset overhead.
All 112 batches pass, with quiet gates, worker joins, fixture
cleanup and persistent archive verification.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[JIT sizes](jit-summary.json) · [JIT output](jit-observations.tar.xz) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and exact patch](procedures.tar.xz) · [Terminal status](terminal-status.json).
