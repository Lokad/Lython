# B1268 direct parser spans

Measured `b190476cd` and `837fae29b` on November 21, 2026 UTC.
The parser computes the full range from existing token endpoints
and materializes one source span. A closing token can extend an
expression span without allocating its own span or computing its
coordinates. Embedded formatted fields keep the original normalized
source coordinates and the same tie rule when ranges begin together.

## Quiet VM

Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 with normal tiering
and GC. Two processes per source warm for at least 16 seconds of
full requests and take seven rotated batches. Compile includes the
compiled owner disposal. Time to result includes one run, mediated
RAM-backed I/O, output and file verification and result disposal.
Process CPU includes fixture reset and request handling. Startup
and final teardown sit outside both guest clocks.

| Complete script and phase | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | ---: | ---: | ---: |
| Generated validator, compilation | −1.05 to +1.76% | +0.16 to +1.95% | 1.61% / 1.21% |
| Generated validator, compile to result | −4.14 to −2.66% | −3.62 to −2.65% | 0.46% / 1.08% |
| Inherited dataclass configuration, compilation | −2.00 to −0.69% | −1.58 to +0.19% | 0.68% / 0.65% |

Ranges cover all four pairings of process medians. Positive values
indicate slower performance. The validator reaches its result
sooner with lower CPU. Standalone compile elapsed is inconclusive
and CPU is slightly higher in that lane. Dataclass elapsed improves
with mixed CPU. Retain the removal of intermediate spans given the
allocation and end-to-end evidence. The comparison establishes
effects on these scripts between Lython revisions.

The local parser phase allocates 5.27% less for postponed factories,
10.23% less for the dataclass and 10.22% less for the validator.
Complete compile allocations fall 0.81%, 1.86% and 2.42%,
respectively. Warm allocations and accounted execution peaks
match. The borrowed allowance returns to baseline.

## Validation

Windows .NET 11 and Ubuntu .NET 10 each pass 71 white-box and
1,265 public Release checks. The initial white-box build identified
a caller of the changed internal span helper. The corrected caller
retains the same coordinate assertions. All 253 compiler diagnostic
probes and 57 code objects agree, including final instruction source
spans, operands and exception regions. Both 32-file images verify the
three CPython output and file goldens.

All 84 timed batches and both machine gates pass in 276 seconds.
Workers join, guest fixtures are removed and the persistent archive
is SHA-verified under the lease. Only the previous prototype's
reconstructible candidate replica is retired.

[Timing](summary.json) · [Local allocations and span comparisons](local-summary.json)
· [Raw observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).
