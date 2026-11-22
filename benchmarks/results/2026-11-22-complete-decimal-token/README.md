# B1338 Parse complete UTF-8 decimal tokens before classification

Private `0ee58f0b5` parses a bounded token directly and requires complete
consumption. Ordinary ASCII tokens bypass the separate NUL, underscore
and ASCII scans. It also uses the same decimal conversion for wide and
normalized inputs. Python NaN and infinity handling, wide-input
checkpoints, scratch admission and original-input errors remain in place.

Compared `e961df912` with `0ee58f0b5` on November 22, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Two workers
per revision supply seven rotated batches each after at least twelve
seconds of warmup. Ranges span all process-median pairings. Warm time
includes mediated RAM-backed I/O, output verification and prior-result
disposal. Time to result adds compilation and code-owner disposal.
Process CPU covers the wider worker request.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Typed ASCII meter report | Warm | −6.80 to −3.67% | −7.66 to −3.96% | 0.11 / 3.24% |
| Explicit-float meter report | Warm | −7.72 to −3.37% | −7.75 to −3.33% | 4.27 / 0.43% |
| Typed Unicode meter report | Warm | +1.85 to +3.65% | +0.97 to +3.14% | 1.14 / 0.62% |
| Plain integer invoice | Warm | −0.27 to +0.78% | +0.96 to +2.51% | 0.76 / 0.29% |
| Typed ASCII meter report | Time to result | −3.86 to +0.56% | −3.17 to +1.00% | 1.39 / 3.17% |

Allocation is effectively flat, admitted peaks match and all allowances
refund. Keep the first variant private. The warm gains in the two ASCII
reports justify one refinement. Limit the new parser to operands with
an ASCII decimal lead and retain the prior conversion after normalization
and for long tokens. The current comparison cannot separate the failed
early parse from the changed normalization-tail conversion in Unicode.
Keep the invoice CPU increase and uncertain time to result visible.

Both platforms pass 279 white-box and 1,124 public focused Release
checks. Both complete worker images match four CPython report goldens
and 676 bit-level decimal, bytes and failure observations. The initial
corpus reused an inconsistent fixture size and was rejected by the
verifier. Its corrected manifest has the exact corpus size. All 140
timed batches pass. Every worker joins and the archive verifies under
lease before release.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Sources and checks](checks-and-procedures.tar.xz) ·
[Desktop attribution](desktop-profile.tar.xz).
