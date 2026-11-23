# B1440 Build literal CSV rows directly from immutable input

`fbef27903` retains the production and test changes measured at
`400297740` against `4b17d8fa2`. A complete literal record is read from
the immutable input string during parsing. When a field needs
transformation or continuation, the parser moves the prefix to its
admitted buffer and continues at the same field. Final rows own their
payload, including after a reader closes. The native file path still
appends physical lines directly to admitted storage.

Only the core DLL differs across both verified 32-file images. The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Borrowed CSV fulfillment | Warm | −8.53 to −2.91% | −8.01 to −1.86% | 4.54 / 1.54% |
| Borrowed typed ASCII meter | Warm | −9.98 to −7.00% | −10.00 to −7.80% | 1.12 / 2.16% |
| Borrowed typed Unicode meter | Warm | −9.33 to −7.85% | −7.46 to −6.27% | 0.16 / 1.48% |
| Native typed ASCII meter | Warm | +0.96 to +2.44% | −1.44 to +2.99% | 0.82 / 0.64% |
| Native typed Unicode meter | Warm | −0.38 to +6.58% | −0.93 to +5.70% | 0.63 / 6.31% |
| Quote-heavy invoice control | Warm | −1.78 to +1.94% | −2.57 to +3.26% | 2.18 / 1.57% |
| Borrowed CSV fulfillment | Compile to result | −11.95 to −2.78% | −12.71 to −2.27% | 10.10 / 0.28% |
| Nonlocal text control | Warm | −3.09 to +7.99% | −2.48 to +6.80% | 9.39 / 1.87% |

Ranges span comparisons of two independent process medians per source.
Each process warms for eight seconds and supplies seven rotated batches.
Warm elapsed includes mediated RAM-backed I/O, output verification and
prior-result disposal. Compile to result also includes compilation
and compiled-owner disposal. Wider CPU includes reset and request
handling. Startup and final teardown are outside elapsed. The borrowing
meters include `readlines()` and should be compared only with the same
script on the other image.

Retain removal of the record copy and the warm CPU gains in all three
borrowed cases. Carry the native ASCII slowdown and inconclusive Unicode,
text and invoice controls into the next integrated comparison. The
fulfillment time-to-result range favors the candidate but the reference
worker spread is 10.10%. Representative local allocation and admitted
peaks are effectively flat. The focused scratch regression removes an
80-byte increase while checking independent retained rows. The shared
allowance returns to baseline after cleanup.

Windows .NET 11 and Ubuntu .NET 10 each pass 576 focused Release checks.
Both images verify nine complete CPython goldens and a 3,375-case CSV
grammar specimen. All 224 quiet batches verify in 441 seconds. Workers
join, guest fixtures are removed and the archive verifies under the lease.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) ·
[Observations](observations.tgz) · [Tests and procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).
