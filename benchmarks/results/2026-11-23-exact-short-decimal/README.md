# B1465 Reuse exact rounding for short decimal floats

`d969c134f` retains the production source built at `d3fd58f80`,
compared with `f9c476e27`. The existing UTF-8 float entry recognizes
short signed decimals, accumulates their exact integer significand
and uses the same binary64 division as JSON. This replaces trimming
and general-number parsing for those tokens. Extended syntax keeps the
existing parser. Both paths check cancellation and use the existing
memory owner.

The two checked 32-file images differ only in the core. The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026
UTC. Each of two independent workers per revision warms for eight
seconds and supplies seven rotated batches. Ranges cover all four
pairings of process medians. Warm elapsed includes applicable mediated
RAM-file I/O, verification and previous-result disposal. Time to result
adds compilation and compiled-owner disposal. Wider CPU also includes
fixture reset and the request protocol. Startup and final teardown sit
outside those clocks.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Typed ASCII meter | Warm | −10.72 to −8.87% | −10.68 to −8.18% | 0.60 / 1.46% |
| Typed Unicode meter | Warm | −2.68 to +3.15% | −1.30 to +4.47% | 1.55 / 4.37% |
| Explicit float meter | Warm | −7.11 to −3.20% | −7.45 to −0.72% | 2.16 / 2.00% |
| JSON Lines export | Warm | −0.96 to +2.49% | −2.63 to +4.11% | 2.99 / 0.48% |
| Quote-heavy invoice | Warm | −2.42 to +0.78% | −1.75 to +2.50% | 3.24 / 0.04% |
| Typed ASCII meter | Compile to result | −11.53 to −8.68% | −9.77 to −6.77% | 2.66 / 0.55% |
| Typed ASCII meter confirmation | Warm | −8.62 to −6.03% | −7.80 to −6.43% | 2.61 / 0.22% |
| ASCII meter with scientific notation | Warm | −2.84 to +4.93% | −2.38 to +6.67% | 4.01 / 3.84% |

Retain the typed ASCII and explicit float gains. The independent B1466
confirmation uses the original meter and one with its same values
written in scientific notation. All 12,288 numeric fields in the
ordinary ASCII meter satisfy the short-decimal predicate. The typed
Unicode and scientific fixtures use the general parser for all
12,288 fields. Their comparisons span zero, with adverse process CPU
pairings preserved. The JSON Lines and quoted invoice controls also
span zero. These are source-pair measurements.

Local managed allocation is effectively flat on all five primary
workloads. Admitted peaks match and each allowance returns to baseline.
Windows .NET 11 and Ubuntu .NET 10 each pass 223 white-box and 1,211
public focused Release checks. The new rounding case compares the
actual binary64 bytes via builtin float and CSV, including the 2^53
boundary, leading and trailing decimal points and signed zero. A
4,119-token compatibility probe matches CPython, including exponents,
wide coefficients, Unicode and invalid input. The primary five and
secondary two output/file goldens match on the exact measured images.

All 168 primary and 56 confirmation batches verify. Workers join,
guest fixtures are removed and the two raw archives verify under the
lease. The first primary staging attempt stopped on available space.
The successful stage retires only working copies whose exact bytes
remain in pushed archives.

[Identity](identity.json) · [Primary timing](summary.json) ·
[Scientific-notation confirmation](exponent-summary.json) ·
[Allocation and allowance](allocation-summary.json) ·
[Fixture coverage](fixture-numeric-coverage.json) ·
[Primary raw observations](observations.tar.xz) ·
[Confirmation raw observations](exponent-observations.tar.xz) ·
[Checks and procedures](procedures.tar.xz).
