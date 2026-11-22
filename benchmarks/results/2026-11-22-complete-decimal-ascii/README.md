# B1339 Parse complete short ASCII decimal tokens

The candidate `6d2fd2907` uses the managed UTF-8 parser on short tokens
with a decimal lead after whitespace and an optional sign. Full byte
consumption is required. The existing grammar checks, normalization
and wide-token cancellation remain available for other inputs. The
change avoids a separate token classification and culture-based
conversion for eligible decimal, integer and exponent forms.

Compared `e961df912` and `6d2fd2907` on November 22, 2026 UTC on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Both revisions used
two workers per phase, at least twelve seconds of warmup per worker
and seven rotated batches apiece. Ranges span the four pairings of
process medians. Warm includes mediated RAM-backed I/O, output
verification and previous-result disposal. Time to result also
includes compilation and code-owner disposal. Process CPU covers
the wider request. Startup and final teardown are separate.

| Complete script | Phase | Elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Typed ASCII meter report | Warm | −10.98 to −2.76% | −10.90 to −2.61% | 1.27 / 7.86% |
| Explicit-float meter report | Warm | −9.28 to −5.23% | −9.39 to −4.51% | 3.68 / 0.74% |
| Typed Unicode meter report | Warm | −8.35 to +0.94% | −8.64 to +0.62% | 9.34 / 0.72% |
| Plain integer invoice | Warm | −0.59 to +1.61% | −0.96 to +0.97% | 0.43 / 1.77% |
| Typed ASCII meter report | Time to result | −8.38 to −5.33% | −6.67 to −4.17% | 3.17 / 0.15% |

Retain the narrower parser. Both warm ASCII reports and time to result
show lower elapsed and CPU in every pairing. One typed-ASCII candidate
worker is slower than the other, so the 2.76% lower elapsed bound is
more informative than the upper gain. The wide Unicode reference
spread leaves the effect uncertain. Unicode CPU against the faster
reference is 0.00–0.62% higher, below the adverse range of the broader
prototype. The unchanged integer invoice spans zero. This comparison
cannot attribute the earlier Unicode regression to a particular code
path. The broader experiment survives alongside this result.
[B1338](../2026-11-22-complete-decimal-token/README.md).

Windows .NET 11 and Ubuntu .NET 10 each pass 279 white-box and 1,124
public focused Release checks. Both 32-file images match four
complete-script goldens and a separate 676-case CPython corpus
covering bit-level results and errors. Local allocation is flat,
accounted peaks match and every execution allowance refunds.
The 140 timed batches verified in 358 seconds. Both quiet gates pass,
workers join and the archive verifies under lease before release.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Images and checks](checks-and-procedures.tar.xz).
