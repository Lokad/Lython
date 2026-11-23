# B1544 complete fixed CSV unpack into consecutive private locals

Private `99fc6186a` transfers an admitted fixed-width row into consecutive
private local slots after the exact arity and execution checks. The compiler
identifies the consecutive stores, so each row reaches the body without
dispatching individual stores or checking the namespace for each field.
Original slot identities move out of the existing staging window. A
published namespace, captured binding or compound target keeps ordinary
assignment. The CSV reader, conversions and admission are unchanged.

| Warm workload | Elapsed change | Process CPU change | Worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Explicit-float meter | −1.65 to −0.44% | −2.62 to −1.30% | 0.64 / 0.58% |
| Typed Unicode meter | −2.60 to +2.13% | −1.04 to +1.03% | 3.49 / 1.32% |
| Quote-heavy invoice control | −0.19 to +2.23% | 0.00 to +4.00% | 1.04 / 1.38% |
| Short Unicode report control | −2.32 to +5.39% | −3.24 to +2.71% | 5.80 / 1.99% |

The measured compiled tapes identify the five-field transfer on both meter
scripts and none on the controls. The explicit meter uses less elapsed
and CPU. Typed Unicode spans zero. The quote control's adverse CPU range
and the spread in the text control leave the retention decision open.
Keep this version private and measure the shared design that also removes
operand reversal and repeated stores from completed parallel assignments.

Compared with `3e5826f7d` on the quiet Azure F4as_v7 running Ubuntu 24.04.4
and .NET 11 RC3 on November 23, 2026 UTC. Four quiet lanes each warm two
processes per source for 12 seconds, with seven rotated timed batches
per worker. The ranges span all pairings of worker medians. Warm reuses
compiled code and includes mediated RAM-backed I/O, goldens and prior
result disposal. Wider CPU includes reset and request handling. Startup
and compilation are outside these clocks.

Both 32-file images match all four CPython goldens. All 112 timed batches,
quiet gates, joined cleanup and persistent archive verify. In the local
allocation diagnostic, managed allocations and admitted peaks match
exactly and shared budgets return to baseline. Windows .NET 11 and
Ubuntu .NET 10 each pass 1,058 focused public Release checks. Follow-up
`558bb990f` adds in-flight cancellation coverage and passes all three
targeted namespace and cancellation cases on each platform. The new
namespace/arity regression also matches CPython.

[Timings](summary.json) · [Tape proof](bindings.json) · [Allocation](allocation-summary.json) ·
[Images](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and changes](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
