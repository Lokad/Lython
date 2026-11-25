# B1706 Shared CSV record pulls

The private patch replaces the separate sync and async CSV source loops
with one loop. It completes buffered file reads, list indexing and
completed guest pulls inline. One continuation handles repeated pending
pulls. The parser still owns the partial record, and the row array passes
to the caller at the same point. File refills and deferred release use the
existing awaited ownership path. The patch stays private.

The reference uses the exact `96d54066a` runtime in B1705. The candidate
contains only the archived source patch. The other 31 files match. Both
images verify against five independent complete CPython goldens. Ubuntu
24.04.4 and .NET 11 RC3 run on the quiet Azure F4as_v7 on November 25,
2026 UTC. Two processes per image contribute seven rotated batches per
phase. Warm elapsed includes mediated RAM-backed I/O, output and file
verification and result cleanup. Wider CPU adds reset and request framing.
Changes span all four pairs of medians. Negative means lower time.

| Complete script | Phase | Elapsed change | Wider CPU change | Elapsed worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| File-fed map | warm-async | -0.90% to +7.16% | -0.59% to +7.32% | 4.39% / 3.59% |
| Quote-heavy invoice | warm | +0.13% to +2.53% | +1.69% to +3.45% | 1.06% / 1.33% |
| Typed meter, list fed | warm-async | -2.16% to +0.60% | -1.95% to +0.66% | 0.54% / 2.28% |
| Shipment callback report | warm-async | -3.14% to -1.19% | -2.21% to -1.34% | 0.95% / 1.05% |
| Unicode text control | warm-async | -6.81% to +4.34% | -4.91% to +3.75% | 1.26% / 10.57% |

Invoice uses 1.69–3.45% more CPU. File-fed map and list-fed meter
cross zero on both clocks, and the unchanged text control has a
10.57% candidate worker spread. Shipment reads its file through
`DictReader` and uses 1.34–2.21% less async CPU. Keep this version
private while testing whether the extra sync ValueTask handoff
accounts for the invoice regression.

Matched-hosting allocation and admission show unchanged peak charges
and full allowance reuse. The file-fed lane differs by 56 bytes of CLR
allocation. A first allocation pass used a different hosting mode for
two cases. Its raw observations are preserved. An attempted matched
pass refused to overwrite those files. The replay uses new filenames.

Windows .NET 11 passes 573 focused tests and the five cases in the
private-record suite. Ubuntu .NET 10 passes 902 focused tests including
the new regression. It exercises a quoted record with two deferred
source pulls, translates a failing pull, then verifies that the next
row succeeds and stays funded through exhaustion. Both quiet gates and
all 140 batches pass. The workers join and the observation archive is
verified under the machine lease.

[Source and image identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal status](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
