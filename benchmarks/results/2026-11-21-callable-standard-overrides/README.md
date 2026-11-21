# B1282 standard function attribute storage

Ordinary functions read names and docstrings from their executable code
object. This prototype replaces four inline override fields with one
optional record. The first standard metadata mutation admits the record.
Later mutations reuse it. Guest-visible `__dict__` storage and the
representation of positional and keyword defaults are unchanged.

Measured November 21, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The reference `a3d0bfc34` uses the exact
production-equivalent `da805131a` image. Candidate `11680b9a0` was measured.
Two independent processes per source warmed for at least eight seconds,
then contributed seven rotated batches each. Changes range over all
process-median pairings. Warm elapsed includes mediated RAM-backed I/O,
goldens and prior-result disposal. Time to result adds compilation and
code-owner disposal. CPU covers the full request. Startup and final
cleanup are outside those guest clocks.

| Complete script | Boundary | Elapsed change | CPU change | Local warm CLR allocation |
| --- | --- | ---: | ---: | ---: |
| Eager rules | Warm | −3.60% to +1.49% | −4.03% to +1.36% | −5.28% |
| Eager nested factories | Warm | +1.41% to +3.43% | +2.10% to +4.05% | −7.07% |
| Postponed nested factories | Warm | −2.08% to +1.84% | −2.10% to +2.93% | −7.71% |
| CSV invoice | Warm | −2.76% to −1.98% | −1.94% to −1.42% | −0.00% |
| Keyword dataclasses | Warm | +0.38% to +1.44% | +0.59% to +1.83% | +0.00% |
| Factory with wrapper metadata writes | Warm | +0.26% to +3.34% | +0.81% to +4.44% | +1.60% |
| Postponed nested factories | Compile to result | −2.96% to −2.71% | −3.94% to −2.10% | — |

Six CPython goldens pass in both complete frozen 32-file images. Windows
.NET 11 and Ubuntu .NET 10 each pass 2,006 focused Release checks. Every
local execution returns its allowance to baseline. Accounted peaks match
except on the wrapper workload, where 2,048 metadata records add 131,072
bytes. All 196 timed batches pass. Workers join, leases close and the
archive verifies with both quiet gates and fixtures removed.

Keep the measured revision private. It allocates 5.28–7.71% less on the three factory scripts and improves postponed time to result by 2.71–2.96%, with lower CPU. Eager nested factories use 2.10–4.05% more CPU, and the wrapper and dataclass controls also use more CPU. Warm postponed timing spans zero. Keep the invoice improvement visible as a separate result. The follow-up a887d3e8a removes the temporary reservation object, which lowers the wrapper allocation increase from 1.60% to 0.69% in local diagnostics while preserving the factory reductions. It passes the first-write admission and later-write reuse checks. Confirm the refined source on the adverse controls and the postponed factory before retaining it.

[Timing](summary.json) · [Allocation](allocation-summary.json)
· [Raw observations](observations.tgz)
· [Source, checks and procedures](checks-and-procedures.tar.xz).
