# B1127 direct stack transfers in the shared instruction loop

Measured `40ab5132a` and prototype `12ba10e92` on the quiet Ubuntu 24.04.4
Azure F4as_v7 with .NET 11 RC3 on November 19, 2026 UTC. The exact 32-file
images and four CPython workload goldens passed local verification. Each
workload used two processes per source and five rotated rounds. Both quiet
gates and all 80 timed responses passed. Workers joined, the archive was
verified under the lease, and guest fixtures were removed.

| Workload, warm execution | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Annotated rule closures | −2.05% to +1.87% | −1.96% to +2.00% |
| Ordinary rule closures | −1.13% to +1.40% | −0.48% to +2.04% |
| Captured rule closures | −1.63% to +2.37% | −1.97% to +1.54% |
| CSV invoice | −2.26% to −1.07% | −3.27% to −0.72% |

Ranges cover all four cross-source process-median pairings. Same-source
elapsed spreads reached 2.45% for annotated rules and 3.01% for captured
rules. All rule timings remain inconclusive. The CSV improvement has
supporting CPU in this run and awaits independent confirmation.

Retained at `47fb0dc84` for the smaller dispatch path. The shared switch
now executes ordinary loads, stores and stack transfers directly and still
uses the existing member lookup and suspension path. The measured sources
passed 997 focused tests per platform on Windows .NET 11 and Ubuntu .NET 10,
including cancellation, observable effects and cleanup. The first
isolated public run exposed a missing test fixture in the artifact layout.
The archive preserves that run and the passing reruns with the fixture.
Windows allocation stayed within 0.02%, accounted peaks matched, and the
shared allowance returned to baseline in every local workload.

Warm elapsed includes execution, mediated RAM-backed I/O, golden checks
and previous-result disposal. Process CPU includes the wider worker
request. This comparison holds CPython goldens fixed and times Lython
revisions. The separate external baseline retains its recorded revision.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Source, checks and procedures](checks-and-procedures.tar.xz).
