# B1667 Carry compiled map callbacks in operand slots

Short compiled map calls borrow the values supplied by the source cursors.
The existing direct evaluator can return a private scalar or a materialized
alias without building a call packet or workspace. When that evaluator
declines, the existing compiled binder consumes the operand span. Wide calls
and arbitrary callables still use the general argument path.

The reference is `ac9f9211b`. The first candidate uses operand slots for all
short map inputs, including native callables that then need a call packet.
Both exact 32-file images match the complete shipment report, a native
map control and a quote-heavy invoice under CPython 3.13.2. Both platforms
and both Release targets pass 421 focused checks, including dynamic truth,
promotion on overflow and alias identity. Shipment allocation is 1,315,542 B
and peak admission 2,817,644 B in both images. Control allocation differs
by at most 56 B, and all allowance balances return to baseline.

## Initial quiet comparison

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Four workers warm for five seconds each and supply seven rotated
batches targeting 0.63 seconds. Ranges cover the four pairings of worker
medians. Warm elapsed includes mediated RAM-backed I/O, output checks and
previous-result disposal. Time to result also includes compilation and
compiled-owner disposal. Wider CPU includes fixture reset and protocol
framing. Process startup is outside these clocks.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment callback report | Warm | −4.82 to −1.05% | −3.29 to −0.76% | 0.26 / 3.70% |
| Shipment native `map(int, …)` control | Warm | +1.23 to +2.75% | +2.69 to +3.18% | 0.58 / 0.91% |
| Shipment callback report | Warm async | −6.02 to −0.42% | −5.91 to −0.50% | 4.69 / 1.21% |
| Quote-heavy invoice | Warm | −1.61 to −0.61% | −2.62 to 0.00% | 0.63 / 0.39% |
| Shipment callback report | Compile to result | −2.44 to +7.23% | −3.29 to +7.67% | 5.81 / 3.88% |

The initial warm shipment improvement accompanies an adverse native-map
control, and time to result is inconclusive. The refinement keeps the
original packet for native callables throughout iteration. The initial
candidate remains private. Both quiet gates, all 140 batches, worker joins,
fixture cleanup and persistent archival verify.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal status](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
