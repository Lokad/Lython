# B1138 prepared local result destinations

R707 encodes a proven plain local destination in the producer's existing
operand fields. This skips reading the following store and selecting its
name or cell policy on that route. Mirrored locals, captured cells and
pending disposal still use the existing consumer. The compiler validates
that a forwarded result occupies one slot.

The reference is `e20604ef4` and the private prototype is `a051b3ff9`.
Windows .NET 11 and Ubuntu .NET 10 each passed 428 public and 424
white-box focused Release checks. Both 32-file images matched five
CPython goldens, and the shared allowance returned to baseline. Allocation
and accounted peaks were identical on the three rule scripts. Allocation
changed by at most 0.16% on the two controls, with identical peaks.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026 UTC. Warm elapsed includes mediated RAM-backed I/O, output and file
verification, and prior-result disposal. Compile-to-result adds compilation
and compiled-owner disposal. Process CPU covers the wider batch boundary.
Ranges compare all four pairs of duplicate process medians.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rules | Warm | −4.48…−3.24% | −5.75…−3.38% |
| Annotated rules | Warm | −3.81…+3.91% | −2.95…+5.23% |
| Captured rules | Warm | −0.52…+1.33% | +0.53…+2.54% |
| CSV invoice | Warm | −6.09…+2.53% | −5.63…+2.94% |
| Generated validator | Warm | −0.67…+1.01% | −0.32…+0.94% |
| Annotated rules | Compile-to-result | −5.23…+1.36% | −6.67…+0.72% |

Keep this source pair private. Ordinary rules improve with CPU support,
but captured rules use more CPU and other elapsed ranges span zero.
The annotated warm reference medians differ by 6.25%. CSV duplicate
reference and candidate spreads are 3.63% and 5.36%. The prototype adds
a branch to the shared consumer and retains both store routes. A design
that removes the common consumer work without adding that branch would
justify another comparison.

All 120 batches passed the goldens. Both quiet gates passed, all workers
joined and the VM verified the persistent archive before releasing the
lease. The exact reference core is the [B1135 measured core](../2026-11-19-native-integer-transport/measured-core.dll.gz).

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Source and checks](checks-and-procedures.tar.xz).
