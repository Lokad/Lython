# B1359 Prepared integer continuations

Compared `2f9f818b9` and `a206d2ab5` on November 22, 2026 UTC on
the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
The compiler marks an adjacent integer continuation during existing
tape emission. Both executable loops use the bit and the runtime
no longer re-derives adjacency and operand placement. There is no
new table, instruction width or traversal. Fallback uses the ordinary
instruction and its original span.

| Script and phase | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Fully supplied helper, warm | −4.19…−2.02% | −3.67…−1.52% | 1.93 / 0.33% |
| Typed ASCII meter, warm control | −10.36…−1.58% | −8.21…−1.53% | 7.65 / 2.00% |
| Mixed factory, warm | −4.49…+3.28% | −3.96…+4.70% | 1.89 / 6.13% |
| Generated validator, compile | −0.71…+0.40% | −1.68…−0.14% | 0.77 / 0.35% |
| Ordinary CSV invoice, warm | −2.11…−0.30% | −2.88…−0.95% | 1.01 / 0.83% |
| Arithmetic range loop, warm control | −0.98…+0.03% | −1.78…−0.72% | 0.80 / 0.21% |
| Quote-heavy policy invoice, warm | −3.01…−0.34% | −2.25…0.00% | 2.32 / 0.42% |
| Mixed factory, compile to result | −3.76…+1.43% | −3.24…+1.66% | 3.28 / 2.05% |

Negative means the candidate uses less time. Ranges span all four
pairings of the two process medians per source. Warm includes mediated
RAM-backed I/O where used, golden verification and prior-result
disposal. Time to result adds compilation and code-owner disposal.
Compilation alone includes owner disposal. Process startup and final
teardown are separate. CPU covers the wider request.

The simplification is retained with measured gains on the fully
supplied helper and ordinary invoice. The quoted invoice has favorable
elapsed and an inconclusive CPU endpoint. Factory timing spans zero,
and the typed ASCII control's 7.65% reference spread prevents
attribution of its favorable reading. Validator and range-loop elapsed
also span zero. Local allocations differ by less than 0.013%, with
matching admission peaks and allowance restored in all cases.

The frozen inspection covers 25 code objects across ten scripts,
including protected, conditional and published continuations. The
tapes and associated metadata match after masking the 11 added
operator bits. Windows .NET 11 and Ubuntu .NET 10 each pass 161
white-box and 286 public checks. The checks include overflow, error
order, cancellation, protocol suspension and admission
refusal. Seven CPython goldens match both complete images. All 224
timed batches verify in 438 seconds. Both quiet gates pass, every
worker joins and the persistent archive verifies under the lease.
Two exact old working DLL copies were retired after verifying their
full local images and archived benchmark evidence.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Executable layout](shape-summary.json) · [Allocation](allocation-summary.json) ·
[Source and checks](checks-and-procedures.tar.xz) ·
[Earlier operand comparison](../2026-11-22-shared-arithmetic-operand/README.md).
