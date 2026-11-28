# B1967 Resolve one receiver per member call during return inference

After checking known callable returns, the frontend resolves a member
receiver once and passes the result to the pattern, match, dictionary
or general resolver. Pattern and match share argument checks. Mutation
analysis keeps the callable contract it already resolved. The change
removes repeat lookups and preserves specialized return precedence.

The complete worker images use `164bb0e36` and the candidate patch.
Only the core DLL differs. Both images match three independent CPython
3.13.16 output and file goldens. Fifteen source cases with and without
the exact signed regex adapter at three allowance sizes produce 90
matching validity, diagnostic and admission results. Every compile
owner refunds its allowance. Windows .NET 10 and 11 and Ubuntu WSL
.NET 11 each pass 1,437 public and 178 white-box focused Release checks.

## Local matched comparison

November 28, 2026 UTC on Ubuntu WSL and .NET 11 RC3. Two workers per
image warm for at least five seconds and provide seven rotated batches.
The ranges span pairings of worker medians. Compile elapsed covers
resident source and previous compiled-owner disposal. Compile to result
adds execution, mediated RAM-backed file I/O, output verification and
result cleanup. Process CPU also includes framing and fixture reset.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Generated report with 48 string and dictionary helpers, 9,559 B source | Compile | −7.62% to −3.87% | −5.23% to −4.66% |
| Configuration edit, 357 B source | Compile to result | −3.43% to +2.61% | −0.46% to +0.67% |
| Integer loop control, 65 B source | Compile | −3.27% to −1.22% | −1.63% to −0.31% |

Generated-report CPU falls with under 0.38% same-image worker spread.
The integer loop control also moves favorably. Configuration spans
zero in both metrics. All 84 batches verify. Workers join, their
temporary fixtures are removed and the persistent archive verifies.

[Timings](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures and flow checks](procedures.tar.xz).
