# B1497 Forward binary results directly to a closure cell

`40e99e6db` uses the existing adjacent result-consumer encoding for a
binary operation followed by its closure assignment. Both runners can
commit a completed result directly into the same cell. The existing
store still handles cleanup, interruption and indirect namespaces.
The short and rebound word reports mark exactly one arithmetic result.
Other workload tapes and all operand capacities match the reference.

The verified 32-file images compare `ee42dc80a` and `40e99e6db` on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3, November 23, 2026 UTC.
Each boundary uses two processes per revision, at least eight seconds of
warmup and seven rotated batches. Ranges pair their medians. Warm elapsed
includes applicable mediated RAM-file I/O, golden verification and prior
result disposal. Compile to result adds compilation and code-owner
disposal. Wider process CPU includes reset and protocol. Startup and
final teardown sit outside elapsed.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Short nonlocal word report | Warm | −5.60 to +1.42% | −5.94 to 0.00% | 2.22 / 5.11% |
| Mixed-width histogram | Warm | −3.73 to +2.79% | −2.69 to +2.52% | 1.10 / 5.62% |
| Fully supplied helper | Warm | +0.27 to +4.04% | +1.38 to +6.01% | 2.71 / 1.02% |
| Short word report with rebound `len` | Warm | −7.73 to +4.54% | −7.45 to +4.55% | 7.10 / 5.79% |
| Short nonlocal word report | Compile to result | −5.84 to −3.58% | −4.60 to −3.53% | 1.40 / 0.99% |
| Quote-heavy invoice | Warm | −4.27 to +1.12% | −3.51 to +1.75% | 5.58 / 0.05% |

## B1498 Reversed worker order

The same images and inputs run with the candidate first, starting with
the fully supplied control. All 84 timed batches and both quiet gates
verify. The workers join, fixtures are removed and the persistent
archive verifies under the lease.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Fully supplied helper | Warm | −1.42 to +0.69% | −1.80 to +0.23% | 1.86 / 0.27% |
| Short nonlocal word report | Compile to result | −10.34 to −2.33% | −10.42 to −2.46% | 8.01 / 0.85% |
| Short nonlocal word report | Warm | −3.73 to +6.40% | −3.91 to +6.90% | 2.80 / 7.51% |

Retain the direct store in `9e0cf58f8`. Time to result improves in both
runs with supporting CPU. The repeat's reference workers vary, so the
magnitude remains uncertain. Warm short text is inconclusive. The
supplied control slowdown did not recur. The original adverse result
remains in the first run. The confirmed comparison adds no CPython ratio.

Windows .NET 11 and Ubuntu .NET 10 each pass 81 white-box and 190
public focused checks. Both images match five independent CPython goldens.
The new suspension case verifies old binding visibility during an
overloaded operator, exact wide arithmetic, current-value identity and
close/refund through following assignment. It also passes on production.
Four workload allocations match locally, the invoice differs by 50 B,
and all five reported execution peaks match. All 168 timed batches and
quiet, worker-join, fixture-removal and archive checks pass. The initial
local tape-inspection attempts used wrong helper arguments. The final
inspection checked the expected core hashes and compared the actual tapes.

[Image identity](identity.json) · [Timings](summary.json) · [Allocation](allocation-summary-six.json) ·
[Tape changes](tape-summary.json) · [Raw timing observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Checks and procedures](procedures.tar.xz) ·
[Confirmation timings](confirmation/summary.json) · [Confirmation raw timing](confirmation/observations.tar.xz) ·
[Exact reference core](../2026-11-23-imported-regex-programs/README.md).
