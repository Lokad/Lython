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
control, and time to result is inconclusive. Both quiet gates, all 140
batches, worker joins, fixture cleanup and persistent archival verify.

## Native-path refinement

The refinement retains the existing native and wide packet while pulling
inputs. Only the short compiled path borrows typed slots. Four workers
repeat the same lanes with a 0.79-second target on the same VM. Both
images match all goldens. Both Release targets pass the same 421 focused
checks on Windows and Ubuntu. Peak admission matches. The allocation
diagnostic varies by at most 212 B, and all allowance balances return to
baseline.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment callback report | Warm | −4.72 to −0.37% | −5.74 to −1.20% | 3.98 / 0.56% |
| Shipment native `map(int, …)` control | Warm | +0.71 to +3.42% | +1.34 to +2.33% | 1.72 / 0.95% |
| Shipment callback report | Warm async | −7.66 to −1.07% | −7.69 to −1.11% | 3.43 / 3.58% |
| Quote-heavy invoice | Warm | −4.04 to +1.67% | −4.55 to −0.07% | 3.69 / 2.18% |
| Shipment callback report | Compile to result | −6.00 to −2.62% | −5.68 to −4.39% | 1.98 / 1.58% |

The shipment gain survives the refinement and time to result improves in
this block. The native control still spends more CPU, even with its
original packet during iteration. Keep both prototypes private. The typed
path duplicates ordered pulls, and the remaining regression weakens the
case for delivery without simplifying callback transport more broadly.
Both quiet gates and the complete 140-batch archive verify after cleanup.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal status](quiet-terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Refined identity](refined/identity.json) · [Refined timings](refined/quiet-summary.json) ·
[Refined allocation](refined/allocation-summary.json) · [Refined observations](refined/quiet-observations.tar.xz) ·
[Refined terminal status](refined/quiet-terminal-status.json) · [Refined procedures](refined/procedures.tar.xz).
