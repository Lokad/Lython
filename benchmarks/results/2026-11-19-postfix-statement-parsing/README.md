# B1129 shared postfix statement parsing

Measured `42055debe` and `571425090` on the quiet Azure F4as_v7 running
Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026 UTC. Both complete
32-file images matched the three CPython workload goldens. Each lane used
two processes per source and five rotated rounds. Both quiet gates and
all 100 timed responses passed. Workers joined, the persistent archive
was verified under the lease, and guest fixtures were removed.

| Workload and boundary | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Generated validator, compilation | −15.64% to −11.81% | −16.47% to −12.15% |
| Generated validator, compile-to-result | −13.02% to −11.94% | −15.07% to −13.22% |
| Configuration manifest, compilation | −10.62% to −8.94% | −11.78% to −8.01% |
| Configuration manifest, compile-to-result | −4.38% to −2.24% | −2.41% to −1.44% |
| CSV invoice, warm execution | −1.21% to +2.98% | +0.64% to +2.74% |

Ranges cover all four cross-source process-median pairings. Validator
compilation reference spread is 4.47%. All four pairings are faster with
lower CPU. The CSV elapsed comparison is inconclusive and its higher CPU
remains an open control. These are comparisons between Lython revisions.

Retained at `573cac2c6`. The statement parser shares one postfix prefix
across target forms and directly uses a completed postfix expression.
General precedence still handles infix and tuple expressions. This removes
three speculative assignment parsers. Both targets passed 1,270 focused
checks, and the exact frozen cores had identical diagnostics on 237 valid
and malformed statement shapes. Scope, cancellation and nesting tests
retain their existing limits.

Local compilation allocation falls 20.58% for the generated validator,
11.80% for the manifest and 11.00% for the CSV script. The borrowed
allowance returned to baseline. Warm execution allocations and accounted
peaks match on all three workloads.

Compilation includes compiled-owner disposal. Compile-to-result also
includes script execution, mediated RAM-backed I/O, golden checks and
result disposal. Startup, request framing, fixture reset and final
teardown are outside guest elapsed. Process CPU uses the wider request.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Source, checks and procedures](checks-and-procedures.tar.xz).
