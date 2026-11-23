# B1531 keep private integers in fixed tuple returns

Retained `48bf2bc2f` fills the existing return window from a short
terminal sequence of exact operands. It preserves published aliases and
declines duplicate or captured local reads. Arithmetic that can call
Python or promote uses the ordinary instructions. The existing fixed
unpack owns admission and publication. The frozen `48956d4b4` runtime
matches the retained source. The reference is `edb36f493`.

| Complete script and boundary | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Helper-driven CSV invoice, warm | −2.74% to −1.70% | −1.54% |
| Helper-driven CSV invoice, compile to result | −2.72% to −1.32% | −3.12% to −2.50% |
| Plain CSV invoice, warm | −0.86% to +0.20% | −0.56% to +1.54% |
| Quote-heavy policy invoice, warm | −4.14% to +2.40% | −3.57% to +2.38% |
| CSV with explicit tuple observation, warm | −0.70% to +1.31% | −1.30% to +2.56% |
| Function-scoped supplied arithmetic, warm | −1.43% to +0.85% | +0.49% to +1.77% |

Ranges span all four worker median pairings. Negative values mean lower
cost. The helper-driven invoice's elapsed worker spreads stay below
0.87%. The function's process CPU points are mildly adverse with
inconclusive elapsed. Quote worker spread reaches 4.26% on the candidate.

The helper-driven and plain invoices each save about 256 KiB of CLR
allocation per run, roughly 7.4%. The helper-driven difference matches
two 32-byte boxes per record. The quoted script selects the same callee
tape, but its mirrored module publishes those integers at assignment.
Its allocation stays unchanged. All six admitted peaks match and budgets
recover. [Prior allocation attribution](../2026-11-23-current-factory-allocation/README.md).

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23,
2026 UTC. Each worker warmed eight seconds and contributed seven
rotated batches. Warm includes execution, applicable mediated RAM-backed
I/O, verification and prior-result disposal. Time to result adds
compilation and code disposal. CPU includes whole-request work.
All 168 batches, the complete images, both quiet gates and joined
cleanup verify. Staging reclaimed one redundant B1522 working core after
verifying its exact copy in the tracked archive.

Both images match six CPython goldens. 814 public and 952 white-box
focused Release tests pass on Windows .NET 11 and Ubuntu .NET 10.
The tests cover preserved aliases, one awaited arithmetic protocol,
integer promotion, cancellation and refusal before the caller's
return slots change.

[Timing](summary.json) · [Identity](identity.json) · [Tape](tape-proof.json) ·
[Allocation](allocation-summary-six.json) · [Observations](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
