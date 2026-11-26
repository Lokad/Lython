# B1806 Read closed starmap operands from the row

The prototype on `cb258fe7f` lets the existing closed return evaluator
read wider exact rows directly. Eligible returns can skip the inline
workspace and its parameter copy. Only operation shapes with no guest
dispatch or admission between row reads use this view. On decline the
ordinary workspace takes the current arguments before executing a guest
truth protocol or otherwise entering the function.

Both frozen 32-file images match four independent CPython output and
file goldens. Windows .NET 11 and Ubuntu .NET 10 each pass 331 public
and 25 white-box focused Release checks. The new case mutates the exact
source list inside a guest truth callback and checks that the function
already holds the original argument. CPython 3.13.2 reproduces the output.

## Allocation and admission

The four complete scripts ran on Windows .NET 11. The five-item script
has 2,048 records. Synchronous CLR allocation and all admitted peaks
match exactly. Async CLR allocation differs by at most 212 B across
the four scripts. All allowances return to baseline after disposal.

| Workload | Mode | CLR bytes, reference → candidate | Peak admitted bytes, reference → candidate |
| --- | --- | ---: | ---: |
| Three-item shipment rows | sync | 1,391,736 → 1,391,736 | 3,467,109 → 3,467,109 |
| Five-item shipment rows | sync | 1,425,016 → 1,425,016 | 3,795,557 → 3,795,557 |
| Zip-to-starmap control | sync | 1,100,280 → 1,100,280 | 2,822,785 → 2,822,785 |
| CSV meter control | sync | 991,808 → 991,808 | 5,171,785 → 5,171,785 |
| Three-item shipment rows | async | 1,391,920 → 1,392,132 | 3,467,109 → 3,467,109 |
| Five-item shipment rows | async | 1,425,200 → 1,425,206 | 3,795,557 → 3,795,557 |
| Zip-to-starmap control | async | 1,100,464 → 1,100,464 | 2,822,785 → 2,822,785 |
| CSV meter control | async | 992,024 → 992,024 | 5,171,785 → 5,171,785 |

## Local timing

Ubuntu WSL runs two workers per source, with three seconds of warmup
and seven rotated timed batches per worker. Ranges span the four
pairings of worker medians. Negative changes mean less time.
Warm elapsed includes mediated RAM-backed I/O, output and file
verification, and previous-result disposal. Process CPU also includes
fixture reset and request work. Startup and compilation stay outside
the timed lanes. All 168 batches completed with workers joined,
fixtures removed and a verified persistent archive.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Five-item shipment rows | Warm async | +0.54% to +3.22% | −0.76% to +6.37% |
| Zip-to-starmap control | Warm async | −8.39% to +2.53% | −7.46% to +2.43% |
| Five-item shipment rows | Warm sync | −1.75% to +0.29% | −2.87% to −0.27% |
| Three-item shipment rows | Warm sync | −0.21% to +4.52% | +0.81% to +4.00% |
| CSV meter control | Warm sync | −11.21% to +5.75% | −11.18% to +6.92% |
| Three-item shipment rows | Warm async | −11.94% to −1.46% | −13.73% to −2.61% |

Keep the prototype private. The changed five-item async script takes
0.54–3.22% more elapsed time, with CPU spanning zero. Its sync process
CPU falls 0.27–2.87% while elapsed spans zero. The three-item control
moves in opposite directions across APIs, and the CSV meter control has
13.21% reference elapsed spread. The measured source change skips stack
work and does not change peak admission. These observations leave the
whole-script benefit uncertain. Use the delivered script's profile to
pursue removal of the per-record list before refining this route.

[Allocation and admission](allocation-summary.json) · [Timing and spreads](summary.json) ·
[Frozen image hashes](identity.json) · [Raw observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Checks, catalog verification and exact patches](procedures.tar.xz).
