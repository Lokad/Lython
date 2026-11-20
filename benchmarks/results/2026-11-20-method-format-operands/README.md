# B1160 direct method-format operands

Ordinary executable `str.format` calls borrow evaluated slots and the
existing keyword labels through the shared renderer. This removes the
intermediate argument packet and its reservation, population and cleanup.
Positional sites also skip rediscovering the absence of keywords. The caller
keeps slots funded through callbacks and pending cleanup. Expanded arguments
keep their existing packet and owner. Both routes use the same field order,
validation, format hooks and template cache.

The measured pair is `f1593aaf5` and `95654712a`. Both 32-file images matched
four CPython output and file goldens. Windows .NET 11 and Ubuntu .NET 10
each passed 740 public and 137 white-box focused Release checks. The added
wide mixed call exercises a real suspension, later argument and selector
reads, completion and cancellation with allowance reuse. The initial Ubuntu
white-box output directory was outside the fixture lookup tree. The same
selection passed from an in-tree output directory.

The integrated formatting change is `89edd9c9d`. It follows the separate
compiler correction `53c4ed6b2` for stale collection shapes after decorators
and eager annotation callbacks. The earlier core rejected the probes that
CPython executed. The correction passed 348 focused public checks per
platform. The integrated Windows Release check passed 68 cases. The timing
pair predates the compiler correction.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. Warm elapsed includes mediated RAM-backed I/O, golden verification
and previous-result disposal. Compile-to-result adds compilation and code
owner disposal. Startup, fixture reset and final teardown are outside guest
elapsed. Process CPU covers a wider batch boundary. Ranges cover all four
comparisons of duplicate process medians.

| Workload | Phase | Guest elapsed change | Process CPU change | Warm allocation change |
| --- | --- | ---: | ---: | ---: |
| Expanded forwarding | Warm | −4.59…−3.58% | −5.99…−4.36% | −8.65% |
| Shipment selectors | Warm | −1.62…+0.25% | −1.20…+1.19% | −1.75% |
| Named formatting | Warm | −1.96…−0.57% | −2.67…−0.95% | −4.12% |
| CSV invoice control | Warm | −2.78…−0.24% | −4.59…−1.32% | 0.00% |
| Expanded forwarding | Compile-to-result | −4.72…−2.01% | −5.02…−2.79% | |
| Shipment selectors | Compile-to-result | −2.38…−1.14% | −1.96…+0.37% | |

The local allocation comparison restores the allowance to baseline and
matches accounted peaks for all four scripts. The lower forwarding timings
have CPU support, with the favorable invoice control limiting attribution.
Named-format movement is smaller than the control's range. Shipment warm
results and compile-to-result CPU are inconclusive. Retain the simpler
argument handoff and measured reduction in allocation. Carry the control
movement into the next integrated performance checkpoint.

All 120 timed batches and both quiet gates passed. Workers joined, fixtures
were removed and the persistent archive verified under the lease. The
reference core is preserved with the [B1159 profile](../2026-11-20-retained-dispatch-profile/measured-core.dll.gz).

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Source and checks](checks-and-procedures.tar.xz).
