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

The adapter stays private. Its [native admission proof](../2026-11-25-regex-050-native-admission/README.md)
covers owned program and working storage, actual refusal, cancellation and
snapshot cleanup. The pinned production package remains 0.4.0. Final
qualification needs staged parse and compile operations, stable equality,
reversed anchored windows and the released immutable package.

[Identity](identity.json) · [Allocation summary](allocation-summary.json) ·
[Verification and diagnostics](procedures.tar.xz).
