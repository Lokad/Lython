# B1270 remove full binding snapshots during invalidation

Compared `406388271` with measured `837fae29b`, the production
source of the parser checkpoint `d09acaf69`. Invalidation visits
dictionary entries directly. It removes disqualified dictionary
facts during the walk and stages only sequence values that change.
Replacements apply after enumeration. The original cache and
length-fact rules and cooperative cancellation stay in place.
[.NET dictionary enumeration contract](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.enumerator?view=net-10.0).

## Complete scripts

Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC.
Two processes per source use at least 16 seconds of warmed requests
and seven rotated batches. Compilation includes code owner disposal.
Time to result adds one run, mediated RAM-backed I/O, verification
and result disposal. Wider process CPU includes reset and request
handling. Startup and final process teardown sit outside guest clocks.

| Script and phase | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Generated validator, compilation | −0.24 to +2.48% | +0.71 to +1.61% |
| Generated validator, compile to result | −2.56 to −0.86% | −2.04 to +0.26% |
| Inherited dataclass configuration, compilation | −4.84 to −2.44% | −4.68 to −1.97% |

Ranges cover four pairings of process medians. Positive changes
indicate slower runs. Dataclass compilation improves with lower CPU.
The validator reaches its result slightly sooner with mixed CPU.
Its compile-only CPU rises and elapsed spans zero.

Local Windows compilation allocations fall 1.33% for the validator,
0.98% for the dataclass and 0.48% for postponed factories. Warm
allocation and accounted execution peaks match. All owned operations
return their shared allowance to baseline.

The synthetic 256-scalar and 48-join control allocates 0.09% more in
a short diagnostic. A separate control widens 256 list bindings in
one scope and allocates 1.96% more. Dense replacements allocate
temporary values together. Retain the removal of repeated full
snapshots for the complete dataclass and validator workloads with
the dense case and validator compile-only CPU costs recorded.

## Validation

Windows .NET 11 and Ubuntu .NET 10 each pass 21 white-box and
827 public Release checks. The new invalidation regression exercises
simultaneous dictionary retirement and nested and ordinary list
widening. It verifies that independent length facts and cache
behavior survive. Across both complete images, all three CPython
output and file goldens, 253 diagnostic probes and final metadata
for 57 code objects agree.

All 84 timed batches and both quiet gates pass in 276 seconds.
Workers join, guest fixtures are removed and the persistent archive
is SHA-verified under the lease. The single prior prototype image
is retired after the exact committed binary and raw archive verify.

[Quiet timings](summary.json) · [Local controls and diagnostics](local-summary.json)
· [Raw observations](observations.tgz) · [Exact source and checks](checks-and-procedures.tar.xz).
