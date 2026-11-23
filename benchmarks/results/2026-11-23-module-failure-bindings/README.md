# B1536 validate the corrected public module lifetime

In `0112138c1`, a plain class method called to format an unhandled
exception could observe a detached module and lose its bindings.
The original Ubuntu delivery caught this through both public APIs.
The new regression also fails on the frozen source because the message
stays empty. [Original delivery](https://github.com/Lokad/Lython/actions/runs/45273867995).

Revision `e4392967b` keeps the already admitted module slots live
until error formatting finishes. The interpreter retires its operands
and joins cleanup first. Windows .NET 11 and Ubuntu .NET 10 each pass
1,426 focused public checks. The added case reads a file asynchronously,
updates a module binding, verifies the diagnostic and output, and checks
file closure and return to the shared allowance baseline.

| Complete script and boundary | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Quote-heavy invoice, warm | −2.75% to +2.99% | −0.75% to +5.17% |
| Short Unicode word report, warm | +0.71% to +5.65% | +1.06% to +5.07% |
| Configuration edit, compile to result | −0.05% to +1.42% | −0.23% to +0.81% |

Ranges span all four process-median pairings against `0112138c1`.
The quote's candidate workers spread 5.40% in elapsed. Both report
clocks go up in this collection. Candidate report workers spread 3.22%
in elapsed, leaving the magnitude uncertain. Retain the correctness fix
and carry the report cost into the next matched call-path comparison.

The quiet Azure F4as_v7 used Ubuntu 24.04.4 and .NET 11 RC3 on
November 23, 2026 UTC. Warm includes mediated RAM-backed I/O, verification
and prior-result disposal. Time to result adds compilation and code
cleanup. Each worker contributes seven rotated batches after eight
seconds of warming. Three goldens, 84 timed batches, the full images,
quiet gates, worker joins and archive checks verify. Staging reclaimed
one redundant working core after verifying its archived copy.

[Timing](summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Source and checks](procedures.tar.xz) ·
[Terminal status](terminal-status.json) ·
[Corrected delivery](https://github.com/Lokad/Lython/actions/runs/45276884116).
