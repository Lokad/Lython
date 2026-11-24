# B1661 Direct returns from borrowed callback arguments

Short, fully supplied positional callbacks use the existing direct return
evaluator when their compiled code permits it. This drops the copy into a
16-slot callee workspace for those returns. The borrowed arguments already
have stable identity. Dynamic truth and arithmetic overflow still enter the
ordinary workspace. Receiver binding, omitted arguments and keyword binding
keep their existing behavior.

The reference uses `694c1645b` with the exact B1659 patch. The candidate
adds only the callback patch to that source. The separately delivered capture
rollback fix is present in the production code used for correctness checks.
On Windows and Ubuntu, 461 focused checks pass on each Release target.
They cover result and receiver identity, once-only guest truth, promotion to
large integers, default binding, cancellation, managed stack and refusal.
Both 32-file timing images verify all three complete CPython 3.13.2 goldens.

The report reads 2,048 shipment records and classifies each through two
policy callbacks in 128-row batches. It uses the returned dictionaries for
identity and totals. The Windows allocation diagnostic measures 1,496,456 B
per report, with a 2,946,636 B peak admission in both images. Allocations
and admission match on the controls, and all allowances return to baseline.

## Quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.
Ranges use all four pairings of worker medians. Workers warm for at least
five seconds and run seven rotated batches with a 0.60-second target.
Warm time covers mediated RAM I/O, output and file verification and
previous-result disposal. Compile to result also includes compilation and
compiled-owner disposal. Wider CPU includes fixture reset and protocol
framing. Process startup is outside both clocks.

| Complete script | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment callback report | Warm | −5.91 to −1.54% | −4.80 to +0.27% | 0.51 / 4.12% |
| Quote-heavy invoice | Warm | −0.49 to +0.31% | −1.05 to +1.06% | 0.08 / 0.72% |
| Shipment callback report | Warm async | −6.73 to −2.56% | −6.35 to −3.23% | 2.03 / 2.40% |
| Defaulted rules | Compile to result | −1.30 to +2.54% | −0.87 to +1.21% | 0.99 / 2.87% |
| Shipment callback report | Compile to result | −4.69 to −2.77% | −3.63 to −2.22% | 0.78 / 1.22% |

Time to result and warm async improve on both clocks. Warm elapsed improves
while CPU remains inconclusive. Both controls span zero. The earlier
defaulted-rule comparisons use different source pairs and retain their
separate attribution debt.

Both quiet gates, all 140 batches, worker joins, fixture cleanup and verified
archival succeed. The procedures include verification of the earlier frozen
copies retired from the VM and the observations preserved before reclamation.

[Identity](identity.json) · [Allocation](allocation-summary.json) ·
[Timings](quiet-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
