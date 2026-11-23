# B1435 Single scan for materialized cased words

Private `8802d4ea4` shares casing and boundary scanning between a prepared
iterator and an ordinary materialized `lower().split()` or `upper().split()`.
The list admits its capacity and all its words together. Each result owns
its bytes. The first prototype allocated a fresh wide working buffer. This
version borrows the execution's existing buffer.

The reference is `f53c32e53` with the exact B1431 core. Only the core differs
across the two verified 32-file images. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Short Unicode materialized report | Warm | −9.83 to −3.41% | −9.07 to −5.07% | 1.41 / 5.62% |
| Short Unicode materialized report | Compile to result | −10.79 to −8.60% | −11.82 to −7.97% | 1.70 / 0.74% |
| Wide materialized report | Warm | −20.16 to −19.93% | −19.59 to −18.88% | 0.12 / 0.17% |
| ASCII materialized report | Warm | −12.88 to −10.23% | −13.10 to −10.33% | 0.46 / 2.57% |
| Immediate nonlocal report | Warm | −0.22 to +2.05% | +0.78 to +2.81% | 1.57 / 0.70% |
| Dense prepared histogram | Warm | −5.97 to +4.83% | −3.14 to +5.06% | 5.29 / 5.88% |
| Ordinary split report | Warm | −2.88 to +3.73% | −2.07 to +4.04% | 1.50 / 5.23% |
| Quote-heavy CSV invoice | Warm | 0.00 to +1.09% | 0.00 to +1.12% | 0.87 / 0.21% |

Ranges span pairings of medians from two independent workers per source.
Each worker warms for eight seconds and supplies seven rotated batches.
Warm elapsed includes mediated RAM-backed I/O, verification and previous-result
disposal. Compile to result also includes compilation and compiled-owner
disposal. Wider CPU includes resets and protocol. Startup and final teardown
are outside elapsed.

The first prototype raises wide-report allocation from 867,584 to 1,637,638
bytes. Reusing the buffer reduces it to 824,414 bytes, 4.98% below the
reference. Wide-report admitted peak falls from 1,508,761 to 1,475,125 bytes.
An independent 132 KiB single-read diagnostic raises allocation from 682,630
to 1,224,180 bytes and admitted peak from 410,770 to 940,333 bytes. Budget
cleanup returns to baseline in all of these diagnostics. Keep this variant
private and bound the additional working buffer before considering delivery.
The immediate iterator also has no use for the materialized word count.

Windows .NET 11 and Ubuntu .NET 10 each pass 693 focused Release checks and
four original-scale construction rollback cases. Both images match seven
CPython goldens and a 134-input Unicode split corpus. A separate regression
reproduces an admission after cancellation during the prepared buffer header
allocation on the reference. The patch checks cancellation before requesting
payload growth. The independent correction and its focused checks are under
procedures. The quiet run verifies 224 timed batches, joins all workers,
unmounts guest fixtures and archives the observations under the lease.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
