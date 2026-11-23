# B1428 Defer allocation of a dynamic singleton default tuple

Private `c3e8cbcf7` prepays the final tuple and its element before
evaluating a compiled singleton positional default. It places the
value in the definition operand and transfers it to the function.
`__defaults__` materializes a canonical tuple with the same object
used by calls. Existing default mutation, decorator handoff and
private unwind keep their ownership boundaries. Machine integers
box earlier so later calls and tuple reads can share identity.

Compared `c5486a9a3` with `c3e8cbcf7` on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3, on November 23, 2026 UTC. Only
the core library differs in the two 32-file images. The reference
has the same production source as `a12b552bc`.

| Complete script | Boundary | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Postponed nested factories | Warm | −0.82 to +5.22% | −1.83 to +4.00% |
| Postponed nested factories | Compile to result | −3.13 to +7.17% | −4.00 to +6.36% |
| Eager nested factories | Warm | −3.02 to +1.90% | −2.89 to +2.92% |
| Factories reading defaults metadata | Warm | −2.91 to +1.70% | −4.58 to +3.13% |
| Fully supplied helper | Warm | −0.73 to +17.06% | −1.42 to +16.94% |
| Quote-heavy CSV invoice | Warm | −0.53 to +0.67% | −1.11 to +1.12% |

Ranges cover pairings of medians from two independent workers per
source. Workers warm for eight seconds and complete seven rotated
batches. Warm elapsed includes applicable mediated RAM-backed I/O,
verification and previous-result disposal. Time to result adds
compilation and code-owner disposal. Wider CPU includes resets
and protocol. Startup and final teardown are outside elapsed.

Keep this design private. The two factory workloads allocate
65,536 fewer bytes per invocation (7.43%). The defaults observer
allocates 65,536 more (5.34%). The controls' allocations are unchanged
and all five admitted peaks and allowance refunds match. One candidate
worker on the fully supplied helper is 17.32% slower than the other,
with reference spread of 0.51%. The factory results span zero, so
the measurement does not settle the elapsed effect. Earlier boxing
is a concrete storage tradeoff. A refinement that preserves unboxed
defaults through the native consumer and can publish one canonical
object would justify a new comparison if it keeps the ordinary
operand and call paths free of additional work.

Windows .NET 11 and Ubuntu .NET 10 each pass 51 white-box and 369
public focused Release checks, including default observation,
aliasing, admission, suspended evaluation and cancellation.
Both frozen images match five CPython goldens. The initial build
exposed an old native eligibility reference. The measured
candidate updates that check to accept the admitted inline value,
and both initial build failures are preserved.

All 168 timed batches and both quiet gates pass in 330 seconds.
Workers join, the guest mount is removed and the persistent archive
verifies under the lease. Staging retires only working replicas
with exact copies already archived. No CPython elapsed was collected.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
