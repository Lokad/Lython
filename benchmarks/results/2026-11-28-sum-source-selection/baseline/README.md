# B1950 Compare the current file-fed map with its original baseline

The exact B1949 candidate runs against `e036d6597`, the reference that
exposed the file-fed regression in B1944. The VM reuses the two complete
immutable 32-file images. Both verify the same four stock CPython 3.13.16
goldens. The one warm `RunAsync` lane uses the B1949 VM, date, runtime
and input. Two workers per image warm for five seconds each and supply
seven rotated batches. Ranges pair all worker medians.

| Workload | Elapsed change | Process CPU change | Within-image CPU spread, reference / current |
| --- | ---: | ---: | ---: |
| File-fed CSV quantity map | −3.78% to +4.56% | −3.10% to +3.16% | 1.91% / 4.46% |

All 28 batches and both quiet gates pass. Workers join, RAM-backed
fixtures are removed and the archive verifies under the lease.
The endpoint comparison is inconclusive. The measured improvement against
`df568572c` remains established, and the older B1944 regression stays in
the record. Close this bounded comparison with both observations.

[B1949 immediate-parent comparison](../README.md) ·
[Original matched regression](../../2026-11-28-cpython-sequence-iterator-checkpoint/matched-control/README.md) ·
[Timing](summary.json) · [Image identity](identity.json) · [Observations](observations.tar.xz) · [Procedures](procedures.tar.xz).
