# B1446 Inline CSV field descriptors

Private revision `2b976d47b` replaces each CSV parser's descriptor list
with four inline slots and an admitted array for wider rows. It copies
descriptors under the shared allowance at replacement and gives the
first complete record the literal scan. The version that timed
retains replacement overlap, clear cancellation points and first
refusal attribution. The current reader remains in production.

Compared `9480463f5` and `2b976d47b` on November 23, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Both revisions
use two worker processes and seven rotated warm batches per process.
Ranges compare all four pairings of the worker medians. Warm includes
guest execution, mediated RAM-backed input, result verification and
prior-result cleanup. Compilation, startup and final teardown are
outside those medians.

| Complete script | Warm elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | ---: | ---: | ---: |
| Native typed ASCII CSV | −1.14 to +11.92% | −2.80 to +11.58% | 1.40 / 11.65% |
| Native typed Unicode CSV | −2.12 to −0.74% | −1.81 to −0.32% | 1.26 / 0.14% |
| Borrowed typed ASCII CSV | +5.44 to +18.30% | +4.57 to +17.88% | 0.64 / 11.49% |
| Quote-heavy invoice | −1.61 to −1.29% | −2.61 to −0.77% | 0.04 / 0.28% |
| Nonlocal Unicode text control | +0.87 to +2.22% | +0.17 to +2.31% | 0.82 / 0.52% |

Borrowed ASCII is adverse across both candidate workers. Candidate
spread limits its estimated magnitude and leaves native ASCII
inconclusive. The unrelated nonlocal text control also takes longer.
The small invoice and Unicode improvements do not justify promotion
with nearly flat CLR allocation, 32 additional admitted peak bytes on
the affected timed cases, and both a descriptor and scanner change in
the same prototype. A future scanner test can isolate where borrowed
ASCII hands off. This trial does not explain the earlier slowdown on
native ASCII.

The exact timed images match five CPython goldens. A wider local set
verifies nine scripts, 3,375 grammar cases and the 1,025-column
overflow specimen. Windows .NET 11 and Ubuntu .NET 10 each pass 125
focused white-box and 437 public checks. The archive retains the initial
tests that failed against estimates for the old descriptor list and the
corrected tests, including overlap refusal with a retained prior row.
All 140 timed batches pass. The workers join, the raw archive verifies
under the quiet lease and the guest fixtures are removed.

[Timing details](summary.json) · [Allocation and allowance](local-allocation.json) ·
[Exact images](source.json) · [Raw observations](observations.tgz) ·
[Source, failures and validation](procedures.tar.xz).
