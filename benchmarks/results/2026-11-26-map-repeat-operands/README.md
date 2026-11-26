# B1811 Retain trailing unbounded repeat operands in short map calls

The candidate on `a1493cfea` retains the invariant
suffix from exact unbounded repeats while creating `map`. Two retained
references replace two cursors and two pulls per shipment row. Other
inputs keep their existing typed cursor loop. The suffix follows all
live pulls and uses the existing short callback window and continuation.
The three shipment fixtures supply 4,096 such operands per execution.

Windows .NET 11 and Ubuntu .NET 10 each pass 66 white-box and
688 public focused Release checks. An independent CPython 3.13.2
case verifies a mutable alias, finite repeat exhaustion, source position
and a short map with only unbounded inputs. The original delayed-source
and delayed-callback cases now also verify the repeated alias and return
the same shared allowance. Both 32-file images match four CPython
output and file goldens.

## Allocation and admission

Windows .NET 11 runs the three 2,048-row shipment variants and the
file-fed map control in both APIs. All allowances return to baseline.

| Workload | Mode | CLR bytes, reference → candidate | Peak admitted bytes, reference → candidate |
| --- | --- | ---: | ---: |
| Three-item shipment | sync | 1,325,656 → 1,323,352 | 3,467,109 → 3,459,685 |
| Five-item shipment | sync | 1,358,936 → 1,356,688 | 3,795,557 → 3,788,133 |
| Bound zip-to-starmap shipment | sync | 1,099,792 → 1,097,438 | 2,822,785 → 2,815,825 |
| File-fed map control | sync | 808,528 → 808,536 | 2,270,071 → 2,270,071 |
| Three-item shipment | async | 1,325,840 → 1,324,224 | 3,467,109 → 3,459,685 |
| Five-item shipment | async | 1,359,120 → 1,356,878 | 3,795,557 → 3,788,133 |
| Bound zip-to-starmap shipment | async | 1,099,920 → 1,097,622 | 2,822,785 → 2,815,825 |
| File-fed map control | async | 808,744 → 808,808 | 2,270,071 → 2,270,071 |

## Local timing

Ubuntu WSL runs two workers per source, with three seconds of warmup and
seven rotated timed batches per worker. Ranges span the four pairings of
worker medians. Negative changes mean less time. Warm elapsed includes
mediated RAM-backed I/O, output and file verification, and previous-result
disposal. Process CPU also includes fixture reset and request work. Startup
and compilation stay outside the timed lanes. All 140 batches completed
with workers joined, fixtures removed and a verified persistent archive.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Five-item shipment | Warm async | −9.97% to −2.93% | −7.84% to −3.57% |
| File-fed map control | Warm async | −9.40% to −6.07% | −10.15% to −4.64% |
| Three-item shipment | Warm sync | −20.76% to −2.52% | −19.69% to −2.27% |
| Bound zip-to-starmap shipment | Warm async | −18.12% to +71.10% | −20.83% to +31.43% |
| Five-item shipment | Warm sync | −5.76% to +0.57% | −7.53% to +0.26% |

## Local repeat

The repeat keeps both source images and runs 84 additional timed batches.
It starts with the file-fed control and warms each worker for six seconds.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed map control | Warm async | −0.75% to +6.67% | −2.06% to +7.14% |
| Bound zip-to-starmap shipment | Warm async | −3.83% to +2.03% | −2.80% to +0.00% |
| Five-item shipment | Warm async | −2.58% to +4.50% | +0.00% to +4.46% |

Keep the candidate private. The three shipment variants each allocate
1,616–2,354 fewer CLR bytes at peak admission reductions of 6,960–7,424 B.
The initial wide async CPU decrease overlaps the improvement in the
file-fed control. That benefit disappears in the repeat, where wide
async CPU spans 0.00% to +4.46% and elapsed still spans zero. The bound
case has mixed elapsed and 0.00–2.80% lower CPU on repeat. The limited
allocation improvement and mixed timing do not justify an extra runtime
route. Keep the displaced cursor work and both observations for later
designs that can remove a larger stage.

[Allocation and admission](allocation-summary.json) · [Timing and spreads](summary.json) ·
[Frozen image hashes](identity.json) · [Raw observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Repeat timing](confirmation/summary.json) ·
[Repeat observations](confirmation/observations.tar.xz) · [Focused checks, catalogs and exact patches](procedures.tar.xz).
