# B1038 and B1039 final UTF-8 method formatting

The retained runtime `b491250cb` writes exact positional text fragments
directly into one admitted UTF-8 result. A cached template identifies this
case before execution. The runtime sizes the rendered fragments, admits their
final storage, and copies with cooperative cancellation checks. This removes
the intermediate builder, its capacity growth, and general rendering for this
case. The ordered renderer continues to handle selectors, nonempty format
specifications, conversions, errors and Python callbacks.

Expanded forwarding improved **4.11–6.31% warm** with lower CPU. The
positional-format variant improved **3.34–5.40% warm** and **2.88–4.59%**
compile-to-result with lower CPU. Expanded compile-to-result elapsed improved
**0.85–4.39%** on confirmation with mixed CPU. The named-format slowdown
did not recur. Positional calls and the f-string report spanned zero on
confirmation. Retain the change for its warm and allocation improvements.

| Collection and workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| B1038 Expanded forwarding, warm | −6.31…−4.11% | −6.51…−4.80% | 0.58 / 1.76% |
| B1038 Expanded forwarding, compile-to-result | −4.78…+0.30% | −6.73…+0.46% | 0.68 / 4.63% |
| B1038 Positional-format forwarding, warm | −5.40…−3.34% | −5.87…−3.16% | 1.37 / 0.79% |
| B1038 Positional-format forwarding, compile-to-result | −4.59…−2.88% | −4.04…−2.67% | 0.07 / 1.72% |
| B1038 Shipment method format, warm | −1.69…+0.12% | −2.33…+0.79% | 0.14 / 1.70% |
| B1038 Named formatting, warm | +0.82…+1.82% | +1.48…+4.85% | 0.36 / 0.62% |
| B1038 Shipment selector format, warm | −0.84…+1.52% | −0.89…0.00% | 1.83 / 0.54% |
| B1038 F-string shipment, warm | −0.66…+5.82% | +0.56…+6.21% | 1.02 / 5.44% |
| B1038 Positional calls, warm | −2.61…+19.31% | −3.25…+19.61% | 1.80 / 20.34% |
| B1039 Named formatting, warm | −3.80…−0.73% | −5.00…−1.84% | 2.68 / 0.50% |
| B1039 Expanded forwarding, compile-to-result | −4.39…−0.85% | −5.48…+0.12% | 0.77 / 2.91% |
| B1039 Positional calls, warm | −1.87…+2.55% | −0.87…+2.09% | 1.72 / 2.74% |
| B1039 F-string shipment, warm | −4.35…+1.84% | −2.09…+0.95% | 4.35 / 2.03% |

The pair compares `e93356adc` with prototype `3744d19c5`, whose runtime
matches `b491250cb`. Verified core hashes begin with `b21ca2e4` and
`ad414955`. Both collections ran 2026-11-19 UTC on the quiet Ubuntu
24.04.4 Azure F4as_v7 with .NET 11 RC3. Ranges cover the four pairings
of process medians. Negative values favor the candidate. The repeat ran
the original case rows and binaries with a changed worker order. Its
named and f-string reference spreads limit any further conclusion about
those controls. Both forwarding cases share fixture and goldens, with
ordinary positional dictionary transport in the positional-format case.

The local Windows allocation diagnostic records **3,131,224 → 2,923,814 B**
for expanded forwarding and **2,557,326 → 2,350,380 B** for the positional
variant, reductions of 6.62% and 8.09%. Accounted peak fell 52,736 bytes
in each. Control allocations differed by at most 0.03%, and their peaks
agreed. All allowance balances returned to baseline.

Windows .NET 11 and WSL .NET 10 each passed 504 focused tests. The
test-only follow-up `f71cbd760` adds four refusal and cancellation cases,
including cancellation triggered during admission, unchanged aliases,
terminal refusal and reuse of the allowance. All ten tests in its
focused suite passed on both targets. A separate CPython 3.13 probe
confirmed repeated Unicode fragments, escaped braces, ordered custom
formatting and missing index behavior.

Both full 32-file images verified against the catalog and CPython outputs
before staging. The nine initial jobs ran in 410.20 seconds and the four
confirmation jobs in 187.07 seconds. All 260 timed batches passed output
and file checks. Both quiet gates passed, workers joined, guest fixtures
were removed and raw archives verified under the lease. Warm includes
mediated RAM-backed I/O, goldens and prior-result cleanup. Compile-to-result
adds compilation and disposal of its owner. Startup, fixture reset,
request framing and final cache teardown are outside elapsed. Process CPU
has a wider boundary. Platform, stress and installed-package checks passed
in the combined delivery with direct definition records.
[Delivery](delivery/README.md).

[B1038 summary](summary.json) · [B1038 raw](observations.tgz) ·
[B1039 summary](confirmation/summary.json) · [B1039 raw](confirmation/observations.tgz) ·
[Prototype core](measured-core.dll.gz) · [Sources, checks and procedures](checks-and-procedures.tar.xz).
