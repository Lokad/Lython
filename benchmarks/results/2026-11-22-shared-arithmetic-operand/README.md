# B1358 One closed arithmetic operand probe

Compared `2f9f818b9` and private `1e6e38634` on the quiet Azure F4as_v7
on November 22, 2026 UTC with Ubuntu 24.04.4 and .NET 11 RC3. Both full
worker images match their recorded source and adapter. The candidate
classifies the first operand once, shares the dispatch in both execution
loops and removes the separate double-operand decoder. The integer
chain and ordered boxed fallback remain. Slot width is unchanged.

| Script and phase | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Typed ASCII meter, warm | −2.91…−0.34% | −2.46…−0.99% | 0.96 / 1.67% |
| Fully supplied helper, warm | +7.45…+11.98% | +7.24…+14.48% | 1.93 / 2.24% |
| Typed Unicode meter, warm | +2.17…+3.80% | +1.87…+3.44% | 1.52 / 0.07% |
| Arithmetic range loop, warm | +18.27…+19.62% | +18.33…+19.45% | 0.64 / 0.50% |
| Explicit-float meter, warm | −2.71…+4.37% | −3.47…+4.17% | 1.98 / 5.20% |
| Ordinary integer invoice, warm | +3.79…+8.25% | +3.96…+9.83% | 0.27 / 4.01% |
| Typed ASCII meter, compile to result | −9.39…+1.04% | −8.86…+0.51% | 6.62 / 4.58% |

Negative means the candidate uses less time. Ranges span all pairings of
the two process medians per source. Warm execution includes applicable
mediated RAM-backed I/O, output verification and prior-result disposal.
Time to result adds compilation and code-owner cleanup. Process startup
and final teardown are outside these timings. CPU covers the wider
request. This pair measures Lython changes. CPython supplies independent
goldens for the measured images.

The candidate remains private. The small benefit on typed ASCII does
not offset the two-digit integer loop and supplied-helper regressions.
The Unicode meter also slows, and the explicit-float and first-use
meter results span zero. The data shows a cost from the changed runtime
as a whole and leaves the exact code generation cost unresolved.
Local allocation is within 0.018% of the reference. Admission peaks
match and all runs restore the shared allowance.

Windows .NET 11 and Ubuntu .NET 10 each pass 153 white-box and 286
public checks, including float identity and signed zero, integer
overflow and admission, missing-operand spans, custom protocols,
cancellation and delayed CSV cleanup. All six CPython goldens match
both frozen images. All 196 timed batches verify in 386 seconds.
Both quiet gates pass and every worker joins. The persistent archive
and fixture removal verify under the existing VM lease.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Allocation](allocation-summary.json) · [Source and checks](checks-and-procedures.tar.xz) ·
[Prior profile](../2026-11-22-post-capture-first-use-profile/README.md).
