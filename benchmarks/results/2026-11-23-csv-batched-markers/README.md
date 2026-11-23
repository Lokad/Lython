# B1511 Reuse a delimiter mask across neighboring CSV fields

Private `553a51ed5` searches one 16-byte span for the delimiter and line
endings and reuses the resulting mask for later fields. The quoted-field
parser keeps its existing path. The mask skips markers inside a consumed
quoted field. Long fields and short tails use the original bulk search.
The prototype allocates no marker storage.

Compared with the integrated B1510 core at `b7fdb4fec` on the quiet Azure
F4as_v7, Ubuntu 24.04.4 with .NET 11 RC3, November 23, 2026 UTC. Both
32-file images verify. Two workers per source warm for eight seconds and
supply seven rotated batches each. Ranges pair their medians. Timings
include mediated RAM-file I/O, golden verification and prior-result
disposal. Compile to result adds compilation and code-owner disposal.
Wider CPU also includes reset and transport. Startup and final teardown
sit outside elapsed.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Typed ASCII meter | Warm | +1.16 to +4.94% | +2.10 to +4.20% | 2.85 / 0.86% |
| Typed Unicode meter | Warm | −0.92 to +6.93% | −1.52 to +7.62% | 0.77 / 7.10% |
| Explicit-float meter | Warm | −1.80 to +2.75% | −2.94 to +2.06% | 2.07 / 2.51% |
| Quote-heavy invoice | Warm | −0.05 to +1.31% | 0.00 to +2.50% | 0.14 / 1.21% |
| Short nonlocal text | Warm | −7.24 to −2.00% | −8.45 to −0.34% | 0.59 / 5.03% |
| Typed ASCII meter | Compile to result | −4.49 to +0.78% | −4.20 to +0.72% | 0.86 / 4.62% |

Keep the scanner private. Its primary ASCII lane takes longer and uses
more CPU. Unicode has one moving candidate worker, and explicit float
and ASCII first use span zero. The unchanged text workload also moves.
There is no observed allocation or admitted-peak benefit to offset the
ASCII cost. Each of the five local workloads restores the allowance and
matches its admitted peak. The largest allocation difference is 168 B.

Windows .NET 11 and Ubuntu .NET 10 each pass 478 selected Release checks,
including CSV stress. Both images match five independent CPython workload
goldens. The updated native-input fixture exercises the comma inside a
quoted field and the CRLF after the row in the same marker window. All
168 timed batches complete in 282 seconds. Both gates pass and workers
join. The persistent archive verifies under the lease after guest
fixtures are removed.

The input regression is retained in `ff407e547`. The production parser
passes all 28 native-input Release checks on each platform with that fixture.

[Identity](identity.json) · [Timing](summary.json) · [Allocation](allocation-summary-six.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Checks and procedures](procedures.tar.xz) · [Exact reference core](../2026-11-23-retained-call-cpython/README.md).
