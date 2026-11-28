# B1918 Share synchronous and asynchronous text-window refill

The candidate removes 65 lines over `a3289e12b`. Both execution modes use the
same window preparation, BOM decision, carry and publication logic. A ready
host read stays in the caller. An incomplete host read enters one refill
continuation in place of the former refill and decode continuations. The
managed decoder, requested byte counts, raw-array replacement admission and
lifetime of an outstanding host borrow are preserved.

Focused Release passes on Windows .NET 10 and 11 and Ubuntu WSL .NET 11.
Each target runs 349 public and 263 white-box cases, including genuinely
pending reads, cancellation, refused admission, retained slices, malformed
input, split UTF-16 boundaries, gzip, CSV, and the installed optional adapter.
Both images verify the five catalog outputs and file effects against CPython
3.13.16. All ten Windows allocation/admission observations restore the shared
budget baseline and have identical peak execution and compiled ownership.
The two sync JSON CLR allocation medians differ by 56 and 230 B, respectively.
The other eight medians match.

## Local timing

November 28, 2026, Ubuntu WSL and .NET 11 RC3, one pinned logical CPU.
Two workers per image warm for at least five seconds each, then supply seven
rotated batches. Ranges span all pairings of reference and candidate worker
medians. Warm elapsed includes mediated RAM-backed I/O, result verification and
previous-result disposal. External process CPU also covers fixture reset and
request framing. Startup and compilation are outside the timed interval.

| Workload | Mode | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Dense nested JSON | Async | −0.53% to +9.35% | +0.46% to +1.90% |
| Quoted CSV | Async | −0.85% to +0.61% | −1.60% to +6.79% |
| Generated definitions (unchanged) | Async | −4.04% to +1.55% | −4.09% to +3.24% |
| Dense nested JSON | Sync | −9.78% to −1.64% | −7.00% to −2.71% |
| Text histogram | Async | +3.00% to +3.80% | +1.51% to +4.47% |
| Sparse JSON Lines | Async | −4.27% to +1.41% | −3.81% to −1.51% |

All 168 first-run batches verify in 218.2 seconds. The same-image repeat
reverses worker placement and retests async text, both dense modes and
the unchanged control.

| Workload | Mode | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Text histogram | Async | −2.33% to +2.01% | −3.34% to +2.98% |
| Dense nested JSON | Sync | −0.54% to +1.82% | −0.44% to −0.10% |
| Dense nested JSON | Async | −3.20% to +1.70% | +0.97% to +3.09% |
| Generated definitions (unchanged) | Async | −0.20% to +2.11% | −1.33% to +2.13% |

All 112 repeat batches verify in 150.3 seconds.

Retain the shared driver for simpler window and EOF handling. The repeat
puts sync dense JSON within 0.44% in CPU and brings async text across
zero. Dense async uses 0.46–1.90% more CPU on the first run and 0.97–3.09%
more on the repeat. The unchanged control crosses zero on both runs and
reaches +2.13% on the repeat. Dense async elapsed crosses zero on both
runs. The observed CPU cost stays visible for the next quiet checkpoint.
These timings cover the comparison host's ready reads. The Release checks
exercise suspension and cleanup separately.

[First timing](summary.json) · [Repeat timing](repeat/summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [First observations](observations.tar.xz) ·
[Repeat observations](repeat/observations.tar.xz) ·
[Patch and focused checks](procedures.tar.xz).
