# B1695 Private inferred CSV headers

The prototype records when an inferred `DictReader` header is still owned
only by the reader and has matched its cached dictionary layout. It skips
both full header scans for that pair. Exposing or replacing the header
ends the proof. Both checks remain around shell admission, which can
reenter the reader. Exposed and explicit headers still compare each key
by identity. The dictionary takes the same funded row array in either path.

The source pair is `f6cf9796c` and the archived prototype. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Ranges span four pairings of process medians from two workers per image
with seven rotated batches each. Warm clocks include RAM-backed mediated
I/O, output verification and result cleanup. The exposed-header and
plain-reader invoices produce the same output and files as the original.
Their source respectively exposes the inferred header before iteration and
uses `csv.reader` with field indexes.

| Workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Inferred-header invoice, warm | −3.34% to −1.74% | −2.59% to −0.45% |
| Exposed-header invoice, warm | +0.34% to +1.83% | +0.60% to +2.80% |
| Original shipment, warm sync | −5.05% to +1.80% | −3.89% to +2.51% |
| File-fed map shipment, warm async | −14.44% to −7.13% | −12.95% to −6.24% |
| Plain-reader invoice, warm | −1.78% to +2.79% | −2.06% to +2.56% |
| Original shipment, warm async | +1.47% to +4.02% | +1.83% to +4.17% |

Keep this patch private. The file-fed async report and inferred-header
invoice improve. The original async shipment and exposed-header invoice
slow down in both clocks. The original sync shipment spans zero and has
5.69% candidate worker spread. These observations do not isolate the cause
of the opposite async effects. Any revised design needs to explain the
extra work for exposed headers and preserve its reentry checks.

Five complete CPython 3.13.2 goldens match. Peak admission and restored
balances match on all five cases. Steady allocation differences are less
than 0.02%. Windows .NET 11 and Ubuntu .NET 10 each pass 500 focused
public cases, and Ubuntu passes 128 white-box cases. These exercise late
header exposure, key identity, retained rows, reentrant validation,
cancellation and terminal admission refusal.

The initial VM preflight lacked room for the frozen image and receipts.
After verifying exact archived duplicates, the corrected launch uses the
same source and catalog. Both quiet gates, all 168 batches, worker cleanup,
fixture removal and archive verification pass.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Procedures and patches](procedures.tar.xz) · [Terminal status](quiet-terminal-status.json).
