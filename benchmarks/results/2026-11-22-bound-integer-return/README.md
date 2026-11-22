# B1385 Evaluate closed integer methods directly from supplied arguments

The frozen `d45f2ed21` and private `179ac1b31` cores ran on
November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3. Both 32-file images verified. Only the core DLL differs.

The quoted policy invoice's rounding method already satisfies the
compiler's closed integer return proof. Bound methods still initialize
and populate a native callee workspace. The prototype lets the existing
chain read slot zero from the evaluated receiver and other local values
from the supplied span. It skips the workspace and native dispatch on
an eligible return. Unsupported receivers, protocols and overflow still
reach the normal body. The ordinary chain now takes a receiver argument
and checks it when reading each operand, including at unbound sites.
[Compiler shape](../2026-11-22-native-direct-integer-return-refined/shape-summary.json) ·
[Whole-script attribution](../2026-11-22-record-text-attribution/README.md).

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Quote-heavy policy invoice | Warm | −3.57 to +2.19% | −2.88 to +2.30% | 0.64 / 5.30% |
| Nested-helper CSV invoice | Warm | −0.28 to +2.75% | −1.52 to +3.44% | 2.92 / 0.11% |
| Function-scoped positional helper | Warm | +4.95 to +6.62% | +4.40 to +6.78% | 1.47 / 0.11% |
| Mixed closure factory | Compile to result | −5.96 to −0.06% | −5.96 to 0.00% | 4.79 / 1.42% |
| Unicode word count | Warm | −1.79 to −0.45% | −2.01 to −0.51% | 0.76 / 0.60% |
| Positional call control | Warm | +0.06 to +3.14% | 0.00 to +1.22% | 1.21 / 1.84% |

Ranges span all pairings of two process medians per source. Each
process warms for eight seconds of full-request time before seven
rotated batches. Warm includes mediated RAM-backed I/O where present,
stdout and file verification, and prior-result disposal. Compile to
result adds host compilation and code-owner disposal. Wider CPU also
includes worker reset and protocol. Startup and final teardown are
outside the elapsed clocks.

Keep the variant private. The bound invoice gain is uncertain and the
unchanged function-scoped helper has a reproducible regression within
this collection. The shared change introduces a possible source of
that cost. The timing alone cannot attribute the result to the new
operand reads. The factory and text directions do not establish a
general improvement. A refinement must keep the ordinary arithmetic
operand reader unchanged while still removing the bound workspace.

Both platforms pass 12 white-box and 410 public focused Release checks.
The new case covers signed arithmetic, overflow, a stored bound method
and reflected receiver behavior. CPython 3.13.2 agrees with it and the
six frozen whole-script goldens. Local allocation is effectively flat,
all peaks match and each allowance refunds. All 168 timed batches and
both quiet gates pass. Workers join and the archive verifies under the
lease. The staged copies retired on the VM have exact archive proofs.

[Timings](summary.json) · [Raw observations](observations.tgz) ·
[Local allocation](allocation-summary.json) · [Source and images](source.json) ·
[Procedures and tests](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
