# B1348 Transfer computed stores while preserving captured arithmetic

Measured `7efd2d7f7` transfers a consumed integer from its executable
slot into the existing capture fields. The cell creates a stable
integer object on observation. Namespace writes and other operands
keep their original materialization. The existing counter arithmetic,
cell charge and scope lifetime stay intact.

Compared with `cc36bd4df` on November 22, 2026 UTC on the quiet Azure
F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Both frozen 32-file images
use the same worker and optional PythonRe 0.4.0 adapter. Two
processes per source have eight seconds of full-request warmup and
seven rotated batches apiece. The first worker order reverses B1347.
Ranges span process-median pairings. Warm execution includes
mediated RAM-backed I/O, verification and prior-result cleanup.
Time to result adds compilation and code-owner disposal. Process
CPU covers the wider request. Startup and final teardown are separate.

| Workload | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Computed nonlocal stores | Warm | −4.57 to −2.51% | −4.36 to −0.81% | 1.16 / 0.99% |
| Existing captured counter | Warm | −5.22 to −2.15% | −4.59 to −1.99% | 1.34 / 1.88% |
| Mixed factory | Warm | −4.36 to −1.82% | −4.26 to −0.99% | 1.65 / 0.99% |
| Fully supplied helper | Warm | −2.90 to +1.50% | −2.16 to +2.00% | 3.79 / 0.71% |
| Computed nonlocal stores | Time to result | −5.84 to +0.74% | −4.34 to +0.71% | 2.10 / 4.79% |

Retain the transfer in this form. The computed report uses
544,576 versus 446,304 managed bytes per local run, a reduction of
18.05%. Its quiet warm CPU and elapsed fall. The counter has
favorable pairings in this run, after the adverse result for the
different B1347 cell representation. The fully supplied helper
and computed-report time to result span zero. The three allocation
controls and all admitted peaks match. All allowances refund.

Windows .NET 11 and Ubuntu .NET 10 each pass 53 white-box and
733 public focused Release checks. Both frozen images match four
independent CPython goldens. Both quiet gates and all 140 timed
batches pass in 274 seconds. The workers join and the archive verifies
under the lease after fixture cleanup. Staging retires only working
core copies whose bytes survive in verified frozen images. The raw
archives are also retained.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
