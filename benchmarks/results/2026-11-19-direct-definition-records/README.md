# B1031 and B1032 direct executable definition records

Compared `9591040fb` with prototype `c5ff4542d` on 2026-11-19 UTC,
using the quiet Ubuntu 24.04.4 Azure F4as_v7 and .NET 11 RC3. The core
hashes start with `697fc32b` and `b21ca2e4`.
The source and tests match retained commit `e93356adc`.

Both rule scripts use the same immutable key tables as R601. The new
definition constructor builds those tables directly from known names,
then admits and fills fresh private values in insertion order. It removes
first-dictionary freezing from this path and the per-record temporary
reservation object. Layout capacity can stay funded after a failed first
definition because the cache retains it. Unpublished values still refund
when the definition unwinds.

| Run and workload | Guest elapsed change | Wider process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| B1031 Unannotated rules, warm | −1.83…−0.86% | −3.67…−1.17% | 0.04 / 0.95% |
| B1031 Unannotated rules, compile-to-result | −1.50…+1.58% | −1.92…+1.23% | 1.59 / 1.51% |
| B1031 Annotated rules, warm | −6.62…+5.99% | −7.91…+7.54% | 2.21 / 11.04% |
| B1031 Annotated rules, compile-to-result | −6.24…−1.98% | −7.01…−2.34% | 3.13 / 1.37% |
| B1031 Reused defaults, warm | −0.18…+1.79% | +0.27…+2.23% | 0.31 / 1.66% |
| B1031 One annotated definition, compile-to-result | −0.51…+1.24% | −2.48…−0.30% | 1.50 / 0.26% |
| B1031 Annotated rules, compilation only | −0.51…+0.53% | +0.52…+1.34% | 0.09 / 0.96% |
| B1031 Expanded forwarding control, warm | +0.82…+1.21% | −1.09…+2.45% | 0.29 / 0.11% |
| B1031 Positional call control, warm | −1.95…−0.29% | −1.22…+0.75% | 0.27 / 1.42% |
| B1032 Reused defaults, warm | −2.80…+12.10% | −3.01…+12.18% | 2.31 / 12.72% |
| B1032 Annotated rules, warm | −5.94…−3.43% | −4.88…−4.15% | 1.49 / 1.15% |
| B1032 Expanded forwarding control, warm | −1.75…+0.69% | −3.68…+0.88% | 1.50 / 0.97% |

The ranges cover all four pairings of process medians. Negative changes
indicate less time. Annotated warm worker spread reached 11.04% in the
first run. The repeat used the same images and case rows with a different
worker order. It found 3.43–5.94% lower annotated warm elapsed and
4.15–4.88% lower CPU. The initial forwarding elapsed increase did not
recur. Reused defaults still span zero, with 12.72% candidate worker
spread in the repeat. The allocation reductions and supported rule
timings justify retention while that control remains unresolved.

The local rule diagnostics fell from 4,272,424 to 4,141,352 allocated
bytes (3.07%) and from 3,127,568 to 2,996,496 bytes (4.19%). Both
accounted peaks stayed equal. First-use allocation fell 64 bytes,
and the other cases were effectively unchanged. All runs returned the
budget to baseline. Windows .NET 11 and WSL .NET 10 each passed 261
public and 60 white-box focused checks. Six independent probes matched
CPython 3.13 through both APIs, including suspension, callback order,
Unicode keys, fresh values and post-publication mutation.

Both 32-file images verified before staging. The two collections took
408.54 and 139.80 seconds. Both quiet gates and all 240 timed batches
passed. Workers joined, guest fixtures were removed and the observations
were verified under their owned leases. Guest warm includes mediated
RAM-backed I/O, output checks and previous-result cleanup. Compile-to-result
also includes compilation and its owner disposal. Startup, fixture reset,
protocol and final cache teardown are outside guest elapsed. Process CPU
covers the wider request.

[B1031 summary](summary.json) · [Raw B1031 observations](observations.tgz) ·
[B1032 summary](confirmation/summary.json) · [Raw B1032 observations](confirmation/observations.tgz) ·
[Retained core image](measured-core.dll.gz) · [Sources, checks and procedures](checks-and-procedures.tar.xz).
