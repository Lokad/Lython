# B1652 Prepare provisional loads before consumer-specific work

This variant follows [B1651](../README.md) and uses the same `4ae7dcc38`
reference. It moves plain constant, name and closure loads into an early path
within the combined block pass. A local that still needs a checked read keeps
its source span and ordinary preparation. The compiler continues to defer
peak accounting until it knows which adjacent loads survive.

The two complete frozen images match all three timed CPython 3.13.2 goldens
and four untimed boundary programs. Thirty sources produce the same 168 code
objects, including all 7,031 instructions, diagnostic spans, exception regions
and storage bounds. Compilation and warm execution allocations, retained
compilation admission and execution peaks match for all three workloads.

| Workload | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated classifier | Compile to result | +1.11 to +1.86% | +0.33 to +2.16% | 0.11 / 0.63% |
| Sparse generated validator | Compilation | −1.40 to +2.42% | −1.80 to +1.38% | 1.05 / 2.79% |
| Configuration manifest | Compile to result | −0.72 to +0.04% | −3.25 to −0.83% | 0.50 / 0.26% |
| Generated classifier | Compilation | −3.80 to +1.42% | −3.83 to +0.00% | 3.96 / 1.41% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Timing boundaries and worker rotation match the initial comparison.
The classifier's time-to-result increase survives all worker pairings.
The sparse CPU range crosses zero, and manifest CPU falls. Neither variant
establishes a broader compilation gain across these scripts. The combined
pass also adds provisional depth and suffix state because the builder emits
instructions after unconditional raises. Keep both patches for reference and
address that source of complexity first.

Windows and Ubuntu each pass 2,652 focused .NET 11 and 127 .NET 10 Release
checks. Both quiet gates, all 112 batches, worker joins, fixture removal and
archive verification pass. Two VM copies of raw B1650 profiles were retired
after their byte hashes matched the independently preserved archive. The
procedures include the custody receipt.

[Images](identity.json) · [Compile allocation](compile-allocation-summary.json) ·
[Execution allocation](allocation-summary.json) · [Timings](quiet-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Source patch, tape comparison, tests and procedures](procedures.tar.xz).
