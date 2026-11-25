# Funded development adapter allocation on complete log searches

Both images run the same Lython `a45b6da7f` core and comparison worker. The
reference uses the repository adapter with PythonRe 0.4.0. The other image
uses the previously checked private admission bridge and the immutable
0.5.0 package pair from producer `61228b20f`. The frozen adapter and package
assembly hashes are recorded with the complete image. Both images match
all four CPython 3.13.2 goldens.

| Complete workload | Reference allocation | Development allocation | Change | Peak admission |
| --- | ---: | ---: | ---: | ---: |
| Compiled log search | 14,082,190 B | 3,511,638 B | −75.06% | 1,178,381 B in both |
| Saved-bound log search | 14,180,554 B | 3,610,070 B | −74.54% | 1,178,381 B in both |
| Module-level log search | 14,278,714 B | 3,708,230 B | −74.03% | 1,178,293 B in both |
| Quote-heavy CSV invoice | 3,280,864 B | 3,280,886 B | +0.0007% | 9,040,266 B in both |

These Windows .NET 11 RC3 diagnostics report the median measured managed
allocation per warmed execution, including result disposal. Each compiled
log script retains 18,528 fewer admitted bytes before execution, and all
allowances return to baseline. The equal execution peaks do not isolate
provider working storage. The diagnostic measures the combined adapter
and backend and does not establish a CPU or elapsed change.

## B1671 Quiet timing

The exact same frozen images run on the Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3 on November 25, 2026 UTC. Each of four rotated workers
warms for five seconds and supplies seven batches with a 0.63-second target.
Ranges cover all four pairings of worker medians. Warm time includes
mediated RAM-backed input, verification and previous-result cleanup.
Time to result adds compilation and compiled-owner cleanup. Wider process
CPU also covers request handling and fixture reset.

| Complete workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Compiled log search | Warm | −19.54 to −18.90% | −20.79 to −19.65% | 0.55 / 0.25% |
| Module-level log search | Warm | −20.63 to −16.99% | −21.37 to −17.07% | 3.55 / 1.00% |
| Quote-heavy CSV invoice | Warm | +0.05 to +2.20% | −1.56 to +1.45% | 2.03 / 0.12% |
| Compiled log search | Compile to result | −20.16 to −19.68% | −20.34 to −19.43% | 0.54 / 0.05% |

The log searches use less elapsed time and CPU. The unchanged invoice's
elapsed range leans adverse with CPU spanning zero and 2.03% reference
spread. Both quiet gates, 112 batches, joined processes, guest-fixture
cleanup and the persistent archive verify. The comparison combines the
adapter and backend. The bound-method form was measured only for allocation.

The adapter stays private. Its [native admission proof](../2026-11-25-regex-050-native-admission/README.md)
covers owned program and working storage, actual refusal, cancellation and
snapshot cleanup. The pinned production package remains 0.4.0. Final
qualification needs staged parse and compile operations, stable equality,
reversed anchored windows and the released immutable package.

[Identity](identity.json) · [Allocation summary](allocation-summary.json) ·
[Verification and diagnostics](procedures.tar.xz) ·
[Timing identity](timing/identity.json) · [Timings](timing/quiet-summary.json) ·
[Timing observations](timing/quiet-observations.tar.xz) ·
[Timing terminal status](timing/quiet-terminal-status.json) ·
[Timing procedure](timing/procedures.tar.xz).
