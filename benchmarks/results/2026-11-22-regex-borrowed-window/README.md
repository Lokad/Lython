# B1330 Borrow an existing index for single regex windows

Single matches reuse a UTF-8 scalar index the original subject already
owns. This removes the walk to interior window bounds without allocating
an index. Cursor capture projection retains its existing implementation.

Compared `318bf0e0a` and `1557706c6` on November 22, 2026 UTC on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Both images use the
optional PythonRe 0.4.0 adapter. Two processes per revision warmed for
at least twelve seconds and supplied seven rotated batches each. Ranges
span the four process-median pairings. Warm elapsed includes golden
verification and prior-result disposal. Process CPU also includes
request handling. Compilation, startup and final teardown are separate.

| Complete warm workload | Elapsed change | Process CPU change | Control spread reference / candidate | Maximum IQR |
| --- | ---: | ---: | ---: | ---: |
| Indexed bounded search | −12.74 to −10.78% | −12.68 to −10.00% | 0.18 / 2.06% | 1.21% |
| Indexed cursor with capture spans | −1.42 to +3.54% | −2.13 to +3.76% | 0.44 / 4.57% | 0.54% |
| Unindexed Unicode cursor | +1.02 to +1.80% | +0.64 to +0.73% | 0.49 / 0.28% | 0.85% |
| ASCII cursor | −2.09 to +2.49% | −2.37 to +2.76% | 2.77 / 1.86% | 1.10% |

Retain the bounded-search improvement with the measured unindexed
cursor cost. Indexed and ASCII cursor timings span zero. The indexed
candidate spread leaves its magnitude uncertain. All four allocation
and accounted peak measurements are identical and allowance returns to
baseline. The first version's larger cursor regressions remain in
[B1328](../2026-11-22-regex-borrowed-index/README.md).

Windows .NET 11 and Ubuntu .NET 10 each passed 67 white-box and 425
public focused Release checks. Both full frozen images passed all four
CPython goldens. All 112 timed batches and both quiet gates passed.
The first staging attempt rejected a catalog with two links where the
proof covered one. No collection ran. The corrected attempt verified
both aliases against their persistent archives. Workers joined and the
new archive verified under the lease before RAM fixtures were released.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
