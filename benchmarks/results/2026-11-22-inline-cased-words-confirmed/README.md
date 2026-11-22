# B1391 Confirm prepared word storage

The quiet Azure F4as_v7 compared the same frozen `d45f2ed21` and
`207b9d567` images on November 22, 2026 UTC with Ubuntu 24.04.4
and .NET 11 RC3. The first worker order is reversed from B1390.
Both images and catalogs verify. The measured source and test patch
is retained at `c097d73db`.
[Original run, source and local allocations](../2026-11-22-inline-cased-word-headers/README.md).

| Complete script, warm | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | ---: | ---: | ---: |
| Word count with nonlocal width | −1.31 to +0.93% | −2.75 to +1.26% | 1.79 / 0.48% |
| Word count with nonlocal counter | −6.21 to −2.07% | −5.64 to −1.48% | 3.08 / 1.29% |
| Quote-heavy policy invoice | +1.50 to +2.54% | +0.72% | 0.35 / 0.67% |
| Dense line, 4,095 words | −0.70 to +0.87% | −0.24 to +2.62% | 1.18 / 0.40% |

Ranges span every pairing of process medians. Two workers per
source each warm for eight seconds and complete seven rotated
batches. Warm clocks include mediated RAM-backed I/O, verification
and prior-result disposal. Wider CPU includes reset and protocol.
Startup and final teardown are outside both clocks.

The counter reverses its initial slowdown. The width result now
spans zero, as does dense-line elapsed. The quoted invoice uses no
prepared-word cursor and takes longer in both runs. Confirmation
supports the slowdown under stable worker controls. The comparison
does not identify its cause. Keep the quote control open before
publication and repeat it at the next call transport comparison.

Retain the single-buffer owner for its simpler cleanup and measured
memory benefit. In the separate verified dense-line diagnostic,
local allocation falls 50.25% and peak admitted memory falls 11.65%.
The ordinary text reports have nearly unchanged allocation. These
measurements establish the storage benefit and leave a general CPU
improvement unproven.

All 112 timed batches and both quiet gates pass. Workers join,
fixtures are removed and the persistent archive verifies under the
lease. Both platforms passed 417 focused Release checks on the same
patch before it was retained.

[Timings](summary.json) · [Raw observations](observations.tgz) ·
[Verified inputs and procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).
