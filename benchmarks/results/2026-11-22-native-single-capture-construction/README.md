# B1354 Construct the first native capture with its callable

Compared `285628a43` with `9cbcb2437` on November 22, 2026 UTC.
A prepared definition with one previously unexposed local can construct
its first cell and callable under a single admission. This bypasses the
general capture walker and publishes the cell only after the combined
commit. Definitions with eager annotation preflight, existing cells,
multiple captures or an invoked frame use their established routes.

Both frozen 32-file worker images run on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3. Two workers per revision each complete
eight seconds of request warmup and seven rotated batches. The ranges
span process-median pairings. Warm includes stdout verification and
prior-result disposal. Time to result adds compilation and code-owner
disposal. Process CPU covers the wider request. The scripts use no
guest file I/O. Startup and final teardown have separate boundaries.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Fully supplied helper | Warm | −4.12 to +0.22% | −3.94 to +0.23% | 3.77 / 0.73% |
| Eager nested factory | Warm | −5.54 to +1.77% | −4.46 to +2.37% | 5.01 / 2.59% |
| Mixed literal-default factory | Warm | −7.56 to −2.11% | −6.76 to −2.50% | 2.97 / 2.84% |
| Default-observing factory | Warm | −3.06 to −0.76% | −2.45 to +0.44% | 1.70 / 0.66% |
| Postponed nested factory | Warm | −4.08 to +2.61% | −5.75 to +4.37% | 4.24 / 2.62% |
| Mixed literal-default factory | Time to result | −13.16 to −8.95% | −13.36 to −9.31% | 4.70 / 0.15% |

Retain the single-cell construction. Both CPU and elapsed fall on
the mixed factory. The eager, postponed and fully supplied controls
span zero. The default observer uses less elapsed time, with CPU
spanning zero. Local run allocation is effectively flat. All admitted
peaks match and allowances refund. The older B1331 general joint
admission experiment remains adverse. This source pair bypasses its
capture traversal for the prepared single-local case.

Windows .NET 11 and Ubuntu .NET 10 each pass 90 white-box and 485
public focused Release checks. Sync and async refusal cases preserve
the definition's first failure and publish no callable. Both frozen
images match five CPython goldens. All 168 timed batches and both
machine gates pass. Collection takes 328 seconds. Workers join and
the persisted archive verifies under the lease after fixture cleanup.
A local staging name collision was corrected before launch.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Sources and checks](checks-and-procedures.tar.xz) ·
[CPU attribution](../2026-11-22-post-default-first-use-profile/README.md).
