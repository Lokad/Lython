# B1383 Reuse Unicode decimal lookup and normalize short float tokens in one pass

The frozen `a4cb961be` and `d45f2ed21` cores ran on November 22,
2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11
RC3. Only the core DLL differs across the two verified 32-file images.
The exact source patch is retained at `f17ba1c9f`.

Short float tokens that need normalization now validate and normalize
in the existing stack buffer in one pass. Typed CSV and builtin
numeric parsing and format indices use one managed BMP digit value
lookup for both validation and conversion. The long-token scratch
admission, supplementary scalar handling, final decimal conversion,
original-input errors and cooperative checks remain in place.
[Attribution](../2026-11-22-record-text-attribution/README.md) ·
[Earlier adverse parser variant](../2026-11-22-complete-decimal-token/README.md).

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Typed Unicode CSV meter | Warm | −3.78 to +1.25% | −2.72 to +1.54% | 2.66 / 2.50% |
| Typed Unicode CSV meter | Compile to result | −4.96 to −2.41% | −5.12 to −2.16% | 0.22 / 2.46% |
| Explicit `float()` CSV meter | Warm | −2.01 to +0.38% | −1.30 to −0.41% | 1.24 / 1.18% |
| Quote-heavy policy invoice | Warm | +0.90 to +1.06% | +0.72 to +1.12% | 0.14 / 0.02% |
| Unicode word-count report | Warm | −2.83 to +7.53% | −2.53 to +7.73% | 5.29 / 5.10% |

The ranges cover all pairings of two process medians per source. Each
process warms for eight seconds of full-request time before seven
rotated batches. Warm includes mediated RAM-backed I/O, verified
stdout and file bytes, and prior-result cleanup. Compile to result
adds host compilation and code-owner disposal. Wider CPU includes
fixture reset and worker protocol. Startup and final teardown are
outside the elapsed clocks.

Retain the shared lookup and shorter normalization path for the
compile-to-result improvement on 4,096 Unicode meter rows. The warm
gain remains uncertain. The quoted invoice uses ASCII integer fields
and shows a small regression with stable workers. The measurements
do not identify a cause. Keep that regression open and include this
invoice in the next native-call comparison. Both word-count source
images have about 5% process spread, leaving its adverse observations
inconclusive. Local allocations differ by less than 0.1%. The admitted
peaks match and all allowances refund.

Both frozen images match four CPython 3.13.2 goldens and the existing
676-case bit-level float corpus. Windows .NET 11 and Ubuntu .NET 10
each passed 273 white-box and 1,418 public focused tests, plus 62
white-box and 413 public integer-focused checks. A separate census
verified the digit value on all 63,488 valid BMP scalars under both
runtimes. Full delivery qualification is tracked separately.

All 140 timed batches and both quiet gates passed. Workers joined,
guest fixtures were removed and the archive verified under the lease.
The first read-only inventory expected a redundant archive already
retired in B1382. The corrected inventory matched all six alias
owners against committed evidence. A local retry also hit the
allocation runner's overwrite guard, so the original observations
were reused unchanged. Only the six VM links to the older core were
retired. Its exact bytes and all raw observations survive in Git.

[Timings](summary.json) · [Raw observations](observations.tgz) ·
[Local allocation](allocation-summary.json) · [Source and images](source.json) ·
[Procedures and tests](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
