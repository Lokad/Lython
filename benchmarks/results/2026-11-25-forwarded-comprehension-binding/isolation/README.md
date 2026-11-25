# B1737i Isolate forwarded comprehension bindings

The isolated candidate retains the existing append operation and changes only
the comprehension binding and scope. The shipment helper still uses 71
instructions and 21 value slots. The projection's current row feeds the
existing expression tape and remains visible in a Python locals snapshot.
The reference is `4e8e60923`. The machine, phases, clocks and sampling match the
[first block](../README.md), with file-fed map measured first in this block.

| Workload | Phase | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment callback report | Warm sync | −3.50% to −1.16% | −3.38% to −0.49% | 2.06% / 0.36% |
| Shipment callback report | Warm async | −8.22% to −4.11% | −5.15% to −4.02% | 3.10% / 1.34% |
| Shipment callback report | Compile-to-result | −4.45% to −1.58% | −3.70% to 0.00% | 0.52% / 2.48% |
| File-fed CSV quantity map | Warm async | +1.53% to +3.21% | +1.20% to +4.39% | 1.50% / 0.15% |
| Unicode text report | Warm sync | −2.95% to +2.59% | −3.11% to +2.73% | 4.65% / 1.00% |

Retain the isolated binding change. It removes the dictionary-backed
namespace and the named store/reload for these projections. Shipment saves
19,712 B in local execution allocation and 1,192 B in compilation allocation.
Its warm CPU falls in both hosting modes, and elapsed through result falls
while CPU reaches zero in one pairing. Admission peaks are identical for
all four measured scripts.

File-fed map still uses more CPU after isolating the binding work, though
the adverse range is narrower than in [confirmation](../confirmation/README.md).
The control runs a CSV cursor, a quantity callback and `sum`. These results
leave its regression unexplained. Keep it as explicit debt for the next
record-construction change, where both scripts can be compared together.
Text remains inconclusive.

Windows .NET 11 and Ubuntu .NET 10 each pass 979 focused Release checks on
this source. The scope observer checks the shadowed local before and after
the first pull. A custom subscription suspends through host I/O and joins
cleanup on cancellation. Both frozen images pass four independent CPython
goldens. All workers joined and the VM archive verified under the lease.

[Identity](identity.json) · [Tape shape](shape-summary.json) ·
[Execution allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Timing summary](quiet-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
