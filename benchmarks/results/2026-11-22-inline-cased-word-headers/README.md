# B1390 Store prepared word lengths beside UTF-8

The quiet Azure F4as_v7 compared frozen `d45f2ed21` with private
`207b9d567` on November 22, 2026 UTC. Ubuntu 24.04.4 runs .NET 11 RC3
with normal tiering and GC. The 32-file worker images differ only in
their core DLL.

The candidate stores each word's length before its normalized UTF-8
bytes in the cursor's existing admitted buffer. It removes the range
array, cursor replacement as ranges grow, transfer of the byte buffer
between cursors, and clearing of the range slots. It preserves the
same scanner, prepayment for all potentially retained words and the
current cursor lifetime. Initial metadata for a nonempty line costs
four bytes. Later lengths use headroom from skipped whitespace.

| Complete script | Boundary | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Word count with nonlocal width | Warm | −5.62 to −3.43% | −6.09 to −4.20% | 0.17 / 2.15% |
| Word count with nonlocal counter | Warm | +1.51 to +5.20% | +1.88 to +5.59% | 0.43 / 3.19% |
| Map-based mixed Unicode report | Warm | −0.50 to +1.32% | 0.00 to +1.86% | 0.59 / 1.23% |
| Quote-heavy policy invoice | Warm | +0.34 to +3.47% | +1.15 to +3.45% | 3.02 / 0.10% |
| Word count with nonlocal width | Compile to result | −2.54 to +1.39% | −4.31 to +2.60% | 3.15 / 0.85% |

Ranges span all pairings of two process medians per source after
eight seconds of warmup and seven rotated batches per process. Warm
includes mediated RAM-backed I/O, stdout and file verification, and
prior-result cleanup. Compile to result adds compilation and
code-owner disposal. Wider CPU includes reset and worker protocol.
Startup and final teardown are outside the elapsed clocks.

On the four ordinary cases, local allocation is effectively flat and
both direct reports lower the admitted peak by 124 bytes. A separate
Windows diagnostic verifies a generated input with 4,095 words in
one line. Allocation falls from 262,344 to 130,504 bytes per invocation
and measured admitted peak from 731,378 to 646,161 bytes. Both images
verify against CPython and all allowances refund. Dense-line elapsed
was outside this quiet run.

Keep the change private pending confirmation. It removes a full
storage stage and improves the width report, while the counter and
invoice timings are adverse. Reverse the initial worker order and
repeat the width report, counter and invoice. Time the dense line at
the same checkpoint to judge the storage benefit alongside CPU.

Both Windows .NET 11 and Ubuntu .NET 10 pass 417 focused Release
checks. Coverage includes Unicode expansion, final sigma, embedded
NUL, reused shorter buffers, overlapping loans, cancellation and
terminal refusal before side effects. All 140 timed batches verify.
Both quiet checks pass, workers join, fixtures are removed and the
persistent archive is verified under the lease.

The procedures also preserve two local call-only controls using the
same three repeating lines and arithmetic with an inline body, value
helper and nonlocal helper. The file-based samples are unstable. In the
memory-only diagnostic, both helper forms take longer than the inline
form on the frozen reference, with substantial worker spread. The
controls establish semantic equivalence and motivate an isolated
source comparison. They give no qualified speedup. The exact compiled
B1388 helper needs five slots and its caller has six unused slots past
the live call arguments. This permits investigating an operand loan
with existing capacity. The local controls themselves have no such
tail capacity and can serve as fallback controls.

[Source and images](source.json) · [Timings](summary.json) ·
[Local report allocations](allocation-summary.json) ·
[Raw observations](observations.tgz) ·
[Tests, dense-line check and controls](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).
