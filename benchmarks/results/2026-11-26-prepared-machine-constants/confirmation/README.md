# B1770 Quiet confirmation of prepared machine constants

Keep the measured constant preparation. Ordinary definitions and Unicode
text use less warm CPU. Each factory also reaches its result sooner in
this comparison. Escaping rules have an inconclusive warm result.
The earlier local observations and adverse results remain visible.
[Initial trial](../README.md).

| Workload and lane | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| File-fed CSV map control, warm | −0.18 to +1.05% | −0.11 to +0.56% |
| Ordinary definitions, warm | −9.10 to −7.09% | −11.83 to −7.53% |
| Escaping rules, compile-to-result | −4.53 to −2.24% | −6.09 to −1.23% |
| Unicode text report, warm | −8.44 to −1.65% | −7.78 to −3.13% |
| Ordinary definitions, compile-to-result | −5.19 to −2.17% | −5.48 to −0.42% |
| Escaping rules, warm | −6.74 to +2.26% | −8.57 to +0.17% |

The November 26, 2026 comparison uses the original df71fc295 source pair
with the same frozen candidate patch as the initial trial. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 with .NET 11 RC3. Two workers per source warm for
five seconds and provide seven rotated batches per lane. The ranges span
all pairings of worker medians. Negative values mean less time. Warm
elapsed includes verification, result cleanup and the applicable mediated
RAM-backed file reads and writes. Time to result also includes compilation
and compiled-owner disposal. Process CPU covers framing and reset.

The file-fed map exercises no successful machine-constant reads. Its
same-source CPU spread is below 0.57% on both images. Escaping rule
candidate workers vary by 6.72% on warm CPU. These ranges support
retention for the other affected paths. The earlier allocation diagnostic
measures 3,776 B more retained compiler admission for ordinary definitions
and unchanged execution peaks. [Allocation](../compile-allocation-summary.json).

Both 32-file images match all four independent CPython output and file
goldens before staging. Both quiet gates and all 168 batches pass.
All workers join, guest fixtures are removed and the persistent archive
hash verifies. Space was recovered from expanded profile files whose
bytes matched their preserved archives. The custody receipt is included
with the procedures.

The delivered source, which also includes shared regex group metadata,
passes 441 white-box and 1,491 public focused Release checks on each
of Windows .NET 11 and Ubuntu .NET 10.

[Timing and controls](summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
