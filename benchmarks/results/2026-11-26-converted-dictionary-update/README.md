# B1763 Complete exact converted dictionary updates in one segment

The existing dictionary accumulation entry recognizes a complete
`totals[key] = totals.get(key, default) + int(row[field])` instruction
sequence. It reads the prior exact integer before resolving the converter,
reads the live field using already admitted storage, and applies a short
decimal conversion under the ordinary call guard. One native table probe
serves the read and store. Intermediate scalars stay in the existing
native integer storage. Unproved inputs use ordinary execution, including
first cache admission, capacity growth and dynamic calls.

Each shipment variant and the derived JSON totals script completes 2,046
of 2,048 updates through the shared segment in each API. The other two
updates use the ordinary path. Three native integer boxes appear at final
observation. The reference takes 2,048 ordinary gets, conversions and
stores, with no lazy dictionary boxes. The file-fed map never enters the
new segment. The initial diagnostic read a result slot after it had been
materialized. A corrected probe reads it before publication. Both probe
versions and their original counts are preserved.

| Target | CLR allocation, reference → candidate | Peak admission, both |
| --- | ---: | ---: |
| Shipment callback report | 1,160,864 → 1,095,488 B | 2,817,644 B |
| Bound shipment report | 1,167,670 → 1,102,288 B | 2,822,785 B |
| JSON service quantity totals | 925,872 → 860,732 B | 1,909,869 B |

The local Windows .NET 11 allocation diagnostic returns the allowance to
baseline in both images. The file-fed control allocates 808,360 B in both
images and its peak is 2,270,071 B. The JSON fixture derives service and
quantity fields from 2,048 shipment records and occupies 78,755 B.
The script has its own independent CPython golden.

| Workload | Warm API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Shipment callback report | Async | −12.07 to −2.69% | −12.98 to −2.06% |
| JSON service quantity totals | Sync | −30.39 to −23.35% | −28.33 to −21.10% |
| Unicode text control | Sync | −4.91 to +4.64% | −3.90 to +3.46% |
| Bound shipment report | Sync | −12.36 to −7.97% | −12.13 to −6.76% |
| File-fed map control | Async | −1.08 to +4.38% | −1.54 to +6.54% |
| Shipment callback report | Sync | −14.57 to −8.43% | −15.28 to −6.98% |

The reference is `26841dca3`, whose runtime source matches `bc93d4d74`.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 26,
2026 UTC. Two workers per source supply seven rotated batches after at
least five seconds of warmup. Changes span the four pairings of worker
medians. Warm elapsed includes mediated RAM-backed I/O, result disposal
and verified output and files. CPU also covers fixture reset and framing.
Both quiet gates passed. All 168 batches completed with joined workers,
removed guest fixtures and a verified persistent archive.

Retain the shared segment. CPU falls on both shipment variants and the
JSON totals. Its precise magnitude varies, particularly on ordinary
async shipment, where candidate CPU worker spread reaches 10.18%. Both
control CPU ranges span zero. The file-fed control has pairings up to
6.54% slower and candidate spread of 7.12%.

Windows .NET 11 and Ubuntu .NET 10 each pass 134 white-box and 540 public
focused Release checks. The public regression agrees with CPython on
retained identity, dynamic mutation and converter rebinding, missing and
invalid fields, and integer promotion. The delayed host actually
suspends, closes both files and returns the allowance. The white-box
regression checks first lookup-cache admission, changes made by the host
during admission and terminal refusal before publication. Its initial
assertion placed admission after the left read. The unchanged reference
reproduced the earlier admission, and the corrected test covers the
observed order. Both full images pass five independent output and file
goldens. The VM also verifies the candidate against the pinned CPython.

Delivery for `7f7c4ed59` passed Windows and Ubuntu Release on .NET 11 and
.NET 10, the original stress scales and installed-package consumers.
[Delivery](https://github.com/Lokad/Lython/actions/runs/45836516565).

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Reference routes](reference-routes.json) · [Candidate routes](candidate-routes.json) ·
[Identity](identity.json) · [Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Patch, tests and procedures](procedures.tar.xz).
