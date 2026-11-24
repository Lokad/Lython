# B1560 convert short Unicode decimals directly

The existing short-decimal parser now reads Unicode decimal digits
and permitted underscores after trimming. It accumulates an exact
integer significand and performs one correctly rounded division.
These tokens skip the normalized ASCII buffer and general decimal
parser. Wide values, scientific notation and invalid syntax keep
their existing parser and error path. The same code serves `float()`
and CSV `QUOTE_NONNUMERIC`.

| Complete warm script | Elapsed change | Wider process CPU change | Worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Typed Unicode meter | −16.88 to −12.09% | −16.17 to −12.06% | 1.99 / 3.70% |
| Typed Unicode meter observing each site | −15.43 to −12.35% | −16.22 to −11.20% | 2.68 / 0.93% |
| Explicit-float meter | +0.43 to +1.78% | −0.76 to +1.23% | 0.33 / 1.01% |
| Quoted invoice | −0.73 to +1.31% | 0.00 to +0.72% | 1.55 / 0.49% |

The two typed workloads show lower elapsed and CPU. All four
scripts have identical CLR allocation and admitted peaks. The
explicit-float elapsed penalty remains visible with mixed CPU.
Retain the shared parser given the typed improvement and recheck the
explicit path when changing short ASCII conversion again.

The reference image `83ae1e2fa` matches the runtime and tests in
`8c88e30d8`. Candidate `5d54f7afb` differs only in the core DLL
within the verified 32-file images. The quiet Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Two
workers per source warm for eight seconds and contribute seven
rotated samples. Ranges span all pairings of their medians. Warm
elapsed includes mediated RAM-file work, verification and prior
result disposal. Process CPU includes reset and request overhead.
Compilation, startup and final teardown are outside warm elapsed.
All 112 batches, quiet gates, worker joins, fixture cleanup and
persistent verification passed.

Both images pass all four CPython 3.13.16 workload goldens and a
separate 432-token comparison of binary64 results and rejection.
The tests cover signed zero, supplementary and mixed Unicode
digits, rounding at the exact significand limit, invalid separators,
conversion before later CSV fields and cancellable long-token
parsing. Windows and Ubuntu each pass 195 white-box and 863 public
focused .NET 11 Release checks. Shared allowances return to baseline
in the separate allocation diagnostics.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Checks, scripts and patch](procedures.tar.xz) · [Terminal receipt](terminal-status.json) ·
[CPU attribution](../2026-11-24-meter-tuple-cpu/README.md).
