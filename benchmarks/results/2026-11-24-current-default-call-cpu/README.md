# B1657 Call and construction CPU after inline default storage

Both profiles use the verified 32-file Lython `6b41db7b3` image from the
[CPython checkpoint](../2026-11-24-inline-defaults-cpython/README.md).
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Each worker verifies its complete golden before and after
sampling, warms for at least eight seconds of guest elapsed time, and is
sampled at 199 Hz for about six seconds. Warm requests reuse compilation.
Compile to result includes compilation, verification, and owner disposal.
These scripts perform no guest file I/O. Startup sits outside the capture.

| Boundary | Invocations | All samples | Resolved runtime ancestry | Under definition construction | Nearest explicit admission | Nearest entry or operand movement | Unmapped leaf |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Warm | 6,656 | 1,196 | 1,192 | 544 | 161 | 122 | 100 |
| Compile to result | 6,656 | 1,206 | 1,171 | 567 | 169 | 108 | 76 |

Definition construction includes its descendant admissions. Of the 161
warm samples nearest `Reserve`, 86 resolve to callable construction and 71
to default storage. Through result those counts are 77 and 85 out of 169.
The remaining samples resolve to the pair constructor as nearest caller.
Those admissions remain distinct to preserve failure order around evaluation.

`CreateClosedPairMember` is the most frequent nearest resolved runtime
method in both lanes, with 249 warm and 261 through-result samples. Its
caller already owns both private functions. The member loop still owns and
then copies a separate default handle into that caller, with a second
rollback path for failure before the function completes. Try a single
pair-owned rollback while preserving admission points, cleanup order and
published aliases. The profile locates the work but cannot split samples
across instructions inside the method or estimate the removable portion.

Both quiet gates, complete goldens, worker joins and fixture cleanup pass.
The first staging attempt stopped at the disk gate. Launch succeeded after
verifying and removing an unpacked copy of the preceding comparison raw
data. The identical archived observations remain on the VM and locally.

[Attribution](attribution.json) · [Image](identity.json) ·
[Observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Procedures](procedures.tar.xz).
