# B1387 Borrow the resolved target slot for a closed integer method

The frozen `d45f2ed21` and private `5b23cbdc0` cores ran on November 22,
2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Only the core DLL differs between the two verified 32-file images.

The candidate runs eligible bound arithmetic directly from the evaluated
operands. It temporarily places the receiver in the resolved target slot,
then restores the target before fallback, exception annotation or operand
retirement. The arithmetic operand reader is identical in both images.
Both managed stack checks remain. The previous variant changed the shared
operand reader and slowed the unbound helper by 4.95–6.62% elapsed.
[B1385](../2026-11-22-bound-integer-return/README.md).

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Quote-heavy policy invoice | Warm | −4.87 to −2.45% | −5.80 to −3.68% | 1.15 / 1.37% |
| Nested-helper CSV invoice | Warm | −2.22 to +0.21% | −3.32 to +1.54% | 2.30 / 0.17% |
| Function-scoped positional helper | Warm | +0.40 to +1.47% | +0.63 to +3.25% | 1.04 / 0.02% |
| Mixed closure factory | Compile to result | −3.13 to +1.57% | −3.63 to +0.99% | 4.51 / 0.33% |
| Positional call control | Warm | +0.67 to +2.74% | 0.00 to +3.86% | 0.58 / 1.47% |

The two processes per source supplied seven rotated batches each after
eight seconds of full-request warmup. The initial worker order is reversed
from B1385. The ranges span every pairing of process medians. Warm elapsed
includes mediated RAM-backed I/O where used, output verification and
prior-result disposal. Compile to result adds compilation and code-owner
disposal. Wider CPU also includes worker reset and protocol. Startup and
final teardown stay outside the elapsed clocks.

Keep the prototype private. The supported quoted-invoice gain is real for
this source pair, and the function-helper regression is smaller than in
B1385. The unchanged positional controls still take longer with stable
workers. The comparison does not assign that remaining cost to a method
or establish that this source caused the earlier regression in B1377.
Resume compiler construction work. Revisit when a design can leave the
common call entry unaffected. Local allocations are effectively flat,
admitted peaks match and every allowance refunds.

Windows .NET 11 and Ubuntu .NET 10 each pass 73 white-box and 410 public
focused Release checks. The initial white-box runs still supplied the old
argument-only span. Updated fixtures supply the original resolved target
with the arguments and preserve all admission and unwind assertions.
Both actual images match six CPython 3.13.2 goldens. All 140 timed batches
verify, both quiet checks pass, workers join and the persistent archive
is verified under the lease.

[Timings](summary.json) · [Raw observations](observations.tgz) ·
[Local allocation](allocation-summary.json) · [Source and images](source.json) ·
[Procedures and tests](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
