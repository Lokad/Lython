# B1749 Feed short zip rows directly to starmap

Short native `zip` rows now fill the existing operand slots in parameter
order for `itertools.starmap`. This removes the per-row tuple, its array and
admission, and the readback into callback operands. Zip shares native pulls,
strict-tail detection and cancellation with ordinary tuple iteration and
fixed unpacking. Each suspended or reentrant pull owns its own partial row.
Retained zip aliases still observe the same cursor and ordinary tuple values.

| Complete script | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Bound `starmap` shipment | Sync | −9.87 to −7.78% | −10.05 to −8.25% |
| Bound `starmap` shipment | Async | −10.35 to −5.10% | −10.00 to −5.56% |
| Shipment retaining zip tuples | Async | −9.75 to −6.87% | −7.85 to −5.27% |
| Shipment with ordinary callbacks | Sync | −4.02 to +0.65% | −2.62 to +0.47% |
| Unchanged file-fed CSV map | Async | −6.54 to −4.98% | −6.69 to −4.27% |

The November 26, 2026 UTC source pair uses the fully identified `cbf8bb3fe`
runtime and the archived patch, with the same comparison workers on the quiet
Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two workers per source warm for
at least seven seconds and provide seven rotated batches. Ranges cover all
four pairings of process medians. Warm elapsed includes mediated RAM-backed
I/O, result checks and disposal. Wider process CPU includes reset and
request framing. Startup and compile-to-result have separate boundaries.

The bound sync gain is consistent while the ordinary routing range spans
zero. The retained-tuple control also uses the shared zip pull loop. The
unchanged file-fed control moves favorably, limiting attribution of the async
elapsed and CPU changes. These timings compare Lython revisions. CPython
supplies correctness goldens, with no external timing ratio in this block.

Local Windows .NET 11 diagnostics show bound shipment allocation falling
from 1,364,272 B to 1,167,876 B, down 14.40%. Peak admission falls from
3,115,985 B to 2,822,785 B, down 9.41%. The three comparison cases have the
same peak admission, with allocation matching within 56 B. Allowances return
to baseline after disposal.

Windows .NET 11 and Ubuntu .NET 10 each pass 558 public and 285 white-box
focused checks. Regressions cover reentrant pulls with retained aliases,
partial and strict-tail read cancellation, suspended callbacks, one close
and allowance reuse. The complete images pass four independent output/file
goldens each. The initial launch stopped before mounting fixtures because its
manifest omitted the CPython hash. The retry verified that executable and
the same images and catalog. Both quiet gates and 140 timed batches pass,
workers join, and the archive verifies before the guest mount is released.
Staging retired one proven byte-identical VM payload preserved locally.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures and patches](procedures.tar.xz).
