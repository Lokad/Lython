# B1684 Borrow one exact call/update operand

The refinement drops B1683's broad forwarded-call change. The integer
call/update borrows the original slot for a single exact local operand and
checks omitted-default eligibility once across reads that cannot invoke
guest code. Other calls keep the original forwarding and workspace. Only
two runtime files change against `c1984f96b`.

Both exact images match eight CPython 3.13.2 goldens, 219 bodies and 2,107
instructions, and all spans and retained compiler admission. Windows
passes 697 public cases per Release target. Ubuntu .NET 11 passes 697
public and 120 white-box cases. Allocation is effectively flat and all
admitted peaks and cleanup balances match. Eager and postponed rules both
have single-local-operand integer update sites. Ordinary and defaulted
definitions have none, although they still run the common native call
code touched by the patch.

## Quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Four workers warm for five seconds each and run seven rotated batches with
a 0.72-second target. Ranges span all four worker-median pairings. Warm
elapsed includes applicable mediated RAM I/O, verification and prior-result
disposal. Time to result adds compilation and compiled-owner disposal.
Wider CPU includes fixture reset and request framing. Startup stays outside
these clocks.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated ordinary definitions | Warm | −4.12 to +3.73% | −4.57 to +3.86% | 4.54 / 3.50% |
| Defaulted rule pairs | Warm | −3.12 to +5.53% | −1.86 to +3.52% | 5.12 / 3.63% |
| Eager annotated rules | Warm | −9.49 to −5.19% | −9.83 to −6.27% | 2.41 / 2.29% |
| Defaulted rule pairs | Compile to result | +1.53 to +3.76% | +2.53 to +3.48% | 0.20 / 2.00% |
| Postponed annotated rules | Warm | +0.21 to +3.50% | +0.76 to +5.02% | 0.20 / 3.08% |
| Quote-heavy invoice | Warm | −1.60 to +0.56% | −2.70 to +0.50% | 0.54 / 1.64% |
| Shipment callback report | `RunAsync` warm | −6.48 to +1.02% | −6.00 to +0.82% | 6.43 / 1.50% |

All 196 timed batches, quiet gates, worker joins and archived receipts
verify. Eager rules improve while postponed rules and defaulted time to
result rise. This repeat does not establish a CPU increase on shipment.
Its reference worker spread is wider than in B1683. Keep B1684 private.
The targeted change does not remove enough call machinery to justify the
adverse complete-script results. The two experiments do not attribute
the timing differences to any specific binder or instruction.

The launch retired five loose VM observation replicas after checking
their byte hashes against archives retrieved locally and verified under
their original leases. All archives are retained. The launch and storage
receipts identify the exact files.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Call/update sites](update-sites.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal status](quiet-terminal-status.json) · [Procedures and source](procedures.tar.xz).
