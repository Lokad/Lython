# B1787 Start integer continuation with the completed first operation

Closed operand returns compute their first machine arithmetic operation
directly. Only a marked tail enters the shared continuation loop. The
ordinary interpreter uses that same loop and keeps its first result when
the tail declines. This removes the separate span overload, the helper's
first-instruction read and recomputation on a declined tail. Each reached
instruction retains its cancellation check. Overflow and Python protocols
still use the ordinary executable tape. [Candidate patch](candidate.patch)

| Complete script | Single-operation returns | Continued returns |
| --- | ---: | ---: |
| Ordinary definitions | 2,608 | 0 |
| Escaping rules | 0 | 3,413 |
| CSV invoice | 0 | 8,192 |
| Protected file search control | 0 | 0 |

The separate instrumented core counts identical routes in both hosting modes.
The benchmark images exclude instrumentation. Windows .NET 11 and Ubuntu
WSL .NET 10 each pass 604 white-box and 1,688 public Release checks. Existing
coverage exercises a carry on either side, overflow, error order, live defaults,
protocol suspension, cancellation and terminal refusal. Four independent
CPython output and file goldens match both complete images. Peak admission
matches and all owners return to baseline. Median CLR allocation matches on
both factories and the control and differs by 50 B on the invoice.
[Routes](routes.json) · [Allocation and admission](allocation-summary.json)

## Initial local timing

The reference is `1fa43def4`. The two 32-file images differ only in the core
DLL. On November 26, 2026 UTC, the local WSL Ubuntu worker uses .NET 11 RC3
and one logical CPU. Two workers per source warm for three seconds then
contribute seven rotated batches targeting 300 ms. Changes span the four
pairs of worker medians. Warm elapsed includes applicable RAM-backed I/O,
output and file verification and previous-result disposal. Wider CPU also
includes fixture reset and protocol framing.

| Script and lane | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| Protected file search, sync | −2.88% to +11.73% | −6.82% to +2.90% |
| Ordinary definitions, async | +2.47% to +25.20% | +1.68% to +14.09% |
| Escaping rules, sync | −31.13% to +0.71% | −11.80% to −0.19% |
| CSV invoice, sync | −27.43% to −3.89% | −16.00% to −8.00% |
| Ordinary definitions, sync | −2.27% to +3.94% | −1.81% to +5.22% |
| Escaping rules, async | −5.28% to +9.18% | −3.08% to +3.64% |

The CSV invoice timing favors the candidate in this block. Async ordinary
definitions run slower. Reference elapsed spread reaches 42.88% on escaping
rules and 31.93% on the invoice. All 168 batches verify, workers join and
the archive verifies after fixture cleanup. [Medians and spreads](summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Procedures and test logs](procedures.tar.xz)

Keep the existing runtime. Both factory CPU ranges span zero in the
quiet confirmation, and the CSV invoice's local improvement does not
reproduce. File search also changes in that block despite taking none of
the changed routes. The candidate removes dispatch work in the source
without a demonstrated whole-script CPU gain. [Quiet confirmation](confirmation/README.md)
