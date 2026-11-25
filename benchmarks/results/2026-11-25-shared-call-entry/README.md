# B1676 Resolve executable call targets before builtin selection

Both executable drivers use the same selection for compiled function
values and bound receivers. A compiled target enters function selection
directly instead of probing range, receiver methods and unary builtins
first. The candidate keeps the existing operand window, admission,
continuation and fallback rules. It is isolated against `01f1f6118`, the
same reference as B1675.

Both complete images match six CPython 3.13.2 goldens, 118 executable bodies,
1,656 instructions, their spans and retained compiler admission. Windows
.NET 11 passes 188 focused public checks. Ubuntu .NET 11 passes the same
188 and six white-box checks. The tests include live defaults and receivers,
retained values, refusal, genuine asynchronous cancellation and cleanup.
Allocation matches to within six bytes per invocation. All admitted peaks
match, and every allowance returns to baseline.

## Quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Two workers per image warmed for five seconds and ran seven rotated batches
with a 0.82-second target. Ranges span all four pairings of worker medians.
Warm execution includes applicable mediated RAM-backed I/O, output checks
and previous-result disposal. The async lane invokes the public `RunAsync`
API. Wider CPU adds fixture reset and request framing. Compilation and
process startup are outside these clocks.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated ordinary definitions | Warm | −3.69 to −2.74% | −4.11 to −3.05% | 0.65 / 0.34% |
| Defaulted rule pairs | Warm | +0.13 to +7.55% | +0.16 to +6.01% | 2.40 / 4.90% |
| Quote-heavy invoice | Warm | −2.49 to +0.56% | −2.31 to +0.00% | 1.72 / 1.39% |
| Shipment routing | Warm | −3.09 to +4.23% | −2.36 to +5.44% | 3.90 / 3.52% |
| Shipment routing | `RunAsync` warm | −0.58 to +1.26% | +0.00 to +1.44% | 1.32 / 0.52% |

All 140 batches, quiet and image gates, worker joins and archive verification
pass. Ordinary definitions use less CPU. Defaulted rules show adverse CPU,
with worker spread limiting its magnitude. Shipment and invoice supply mixed
results. Keep the source private. A follow-up needs a change that removes
operand transport or redundant work without shifting it to other call shapes.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal status](quiet-terminal-status.json) ·
[Source and procedures](procedures.tar.xz).
