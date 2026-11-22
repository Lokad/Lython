# B1347 Transfer integer slots into captured bindings

Measured `cda533638` replaces the cell's separate integer fields
with its existing executable value slot. A consumed integer stays
unboxed when it moves into a standalone capture. Observation publishes
one stable identity. Namespace writes and borrowed tuple items still
materialize before publication. The cell allowance is unchanged.

Compared with `f44a3c462` on November 22, 2026 UTC on the quiet Azure
F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Both frozen 32-file images
use the same worker and optional PythonRe 0.4.0 adapter. Two
processes per source have eight seconds of full-request warmup and
seven rotated batches apiece. Ranges span process-median pairings.
Warm execution includes mediated RAM-backed I/O, verification and
prior-result cleanup. Time to result adds compilation and code-owner
disposal. Process CPU covers the wider request. Startup and final
worker teardown are separate.

| Workload | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Computed nonlocal stores | Warm | −5.16 to +0.31% | −5.74 to −0.48% | 5.21 / 0.54% |
| Existing captured counter | Warm | +5.80 to +11.80% | +5.64 to +11.39% | 1.10 / 4.52% |
| Mixed factory | Warm | −4.04 to +0.04% | −4.22 to +0.54% | 1.63 / 2.58% |
| Fully supplied helper | Warm | −3.87 to −1.48% | −4.99 to −0.29% | 2.35 / 0.14% |
| Computed nonlocal stores | Time to result | −0.91 to +2.38% | −2.16 to +2.22% | 0.11 / 3.20% |

Keep the runtime candidate private. The computed store uses less
warm CPU and allocates less, while its elapsed and time-to-result
ranges span zero. The existing captured counter's CPU rises across
all process pairings. The regression outweighs the reduction in
duplicated representation in this form. The cause is unresolved.
Revisit with a design that keeps the existing scalar-update path and
still displaces boxing on computed stores. Preserve the independent
identity checks.

In the complete text report with repeated computed nonlocal stores,
local managed allocation falls from 544,576 to 446,360 bytes per
run, a reduction of 18.04%. The existing nonlocal counter, mixed
factory and fully supplied helper allocate the same amount on both
images. All accounted peaks match and all allowances refund.

Windows .NET 11 and Ubuntu .NET 10 each pass 51 white-box and 733
public focused Release checks. Two additional sync/async cases extend
the existing retained-identity test to computed stores. They check
current values and identity, unchanged older aliases, clean managed
depths and allowance reuse. The measured runtime source is unchanged
by the test-only follow-up `e8c11d2ce`. The new checks pass on both
sources and both platforms. Four CPython goldens match both worker
images. The initial sparse checkout omitted the public fixture's
benchmark sources. Validation passed after restoring those files.

All 140 timed batches and both quiet gates passed in 275 seconds.
The workers joined and the persistent archive verified under the
lease after fixture cleanup. Staging retired only working aliases
of the B1343 core whose exact bytes survive in its pushed archive.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
