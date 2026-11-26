# B1809 Admit the final executable list directly

A list display funds its fixed-size final array and its owner with one
reservation. The prototype replaces the per-display temporary lease
object with the same direct reserve, commit and rollback sequence already
used for tuples. The owner adopts the pre-funded backing directly without
running the empty-list constructor first. Generator yields and callback
dispatch keep their existing behavior. CSV and text operations use the
same array-adoption constructor. The source pair starts at `0adabc424`.

Both frozen 32-file images match four independent CPython output and
file goldens. Windows .NET 11 and Ubuntu .NET 10 each pass 390 white-box
and 955 public focused Release checks. The existing branch-list
regression exercises terminal refusal and cancellation from the admission
callback, and verifies the original span and the returned allowance.

## Allocation and admission

The four complete scripts ran on Windows .NET 11. Each shipment script
processes 2,048 records. All allowances return to baseline after disposal.

| Workload | Mode | CLR bytes, reference → candidate | Peak admitted bytes, reference → candidate |
| --- | --- | ---: | ---: |
| Three-item shipment rows | sync | 1,391,736 → 1,325,656 | 3,467,109 → 3,467,109 |
| Five-item shipment rows | sync | 1,425,016 → 1,358,936 | 3,795,557 → 3,795,557 |
| Zip-to-starmap control | sync | 1,100,342 → 1,099,736 | 2,822,785 → 2,822,785 |
| CSV meter control | sync | 991,808 → 991,814 | 5,171,785 → 5,171,785 |
| Three-item shipment rows | async | 1,392,132 → 1,325,840 | 3,467,109 → 3,467,109 |
| Five-item shipment rows | async | 1,425,200 → 1,359,120 | 3,795,557 → 3,795,557 |
| Zip-to-starmap control | async | 1,100,464 → 1,099,920 | 2,822,785 → 2,822,785 |
| CSV meter control | async | 992,024 → 992,024 | 5,171,785 → 5,171,785 |

## Local timing

Ubuntu WSL runs two workers per source, with three seconds of warmup and
seven rotated timed batches per worker. Ranges span the four pairings of
worker medians. Negative changes mean less time. Warm elapsed includes
mediated RAM-backed I/O, output and file verification, and previous-result
disposal. Process CPU also includes fixture reset and request work. Startup
and compilation stay outside the timed lanes. All 168 batches completed
with workers joined, fixtures removed and a verified persistent archive.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Five-item shipment rows | Warm async | −2.75% to +1.26% | −5.86% to +1.89% |
| Zip-to-starmap control | Warm async | +2.84% to +42.04% | −11.70% to +40.62% |
| Five-item shipment rows | Warm sync | −1.96% to +5.22% | −3.33% to +4.93% |
| Three-item shipment rows | Warm sync | −5.24% to +11.45% | −0.86% to +10.55% |
| CSV meter control | Warm sync | −10.82% to +1.36% | −9.36% to +1.68% |
| Three-item shipment rows | Warm async | −4.34% to −1.27% | −6.72% to −1.67% |

## Two-lane local repeat

The repeat keeps both complete source images and runs 56 more timed
batches for the wide sync case and the variable zip control.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Zip-to-starmap control | Warm async | −6.69% to −0.74% | −7.80% to −4.79% |
| Five-item shipment rows | Warm sync | −1.75% to +4.17% | −3.23% to +2.59% |

Retain the simpler admission path. Each shipment allocates about 66 KB
less, or 4.64–4.76%, at the same admitted peak. The short async run uses
1.27–4.34% less elapsed time. Wide elapsed and CPU span zero in both
initial lanes and the wide sync repeat. The initial zip control used
2.84–42.04% more elapsed time, with 31.64% spread between candidate
workers. It reverses in the repeat. Both runs remain in the evidence,
and broader throughput remains uncertain.

[Allocation and admission](allocation-summary.json) · [Timing and spreads](summary.json) ·
[Frozen image hashes](identity.json) · [Raw observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Repeat timing](confirmation/summary.json) ·
[Repeat observations](confirmation/observations.tar.xz) · [Checks, catalog verification and exact patch](procedures.tar.xz).
