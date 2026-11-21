# B1304 direct checked layout into the executable tape

The candidate assigns storage slots and physical targets into a
provisional tape in the existing checked operand pass. When every
block is reachable, that tape becomes the final code directly.
Unreachable blocks require an exact replacement tape, admitted
overlap and a second retarget pass. Unreachable branch tails inside
live blocks keep their original instructions and physical targets.

Compared `d51891009` with `77ae31406` on November 21, 2026 UTC
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Two independently warmed processes per revision contributed seven
rotated samples each. The ranges span all process-median pairings.
Compilation includes the compiled owner's disposal. Time to result
includes compile, mediated RAM-backed I/O, golden checks and prior
result disposal. CPU covers the wider request. Startup and final
teardown are separate.

| Complete script | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Generated validator | Compile | −2.26% to −0.65% | −3.43% to +0.44% |
| Generated validator | Compile to result | −2.68% to −0.91% | −1.76% to −1.18% |
| Postponed nested factories | Compile | −1.42% to −0.83% | −2.51% to −0.91% |
| Inherited dataclasses | Compile | −3.49% to +0.31% | −3.45% to −0.09% |

All 112 timed batches and both quiet gates passed. Workers joined,
guest fixtures were removed and the durable archive verified under
the lease. Both complete 32-file images passed the three CPython
goldens. The original Windows timing is inconclusive because the
identical-image controls moved substantially.

The Windows and Ubuntu focused regressions on the local source pair
`480c89e6b` and `672a5678a` pass 385 white-box and 1,250 public tests
per platform. The compiler sources are identical across the two
source pairs. The 61 inspected code objects match exactly, including
the protected assertion tail, physical slot and branch addresses,
diagnostic spans and exception regions. All compile owners refund.
The shape-only probe finds 2,888 final instructions, 576 retargets
and 481 reachable blocks in the largest validator body. There are
no unreachable blocks in the three representative scripts.

Keep the current compiler layout. The prototype shows a small
validator time-to-result improvement and lower CPU for several
lanes. It adds a replacement allocation, a relocation pass and
~16.6 KB of CLR allocation on the dead-heavy control, which emits
513 discarded instructions. A refinement should prepare exact
block reachability before allocating the tape without introducing
another per-instruction inventory.

The initial VM staging attempt requested an archived duplicate
that had already been retired. Rebased the same compiler source
onto the resident production image. A local payload check then
caught a stale copy path, which was corrected before staging.
Both failed setup conditions are preserved with the procedures.

[Timing](summary.json) · [Compile allocation](compile-allocation-summary.json) ·
[Local timing](micro-summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz).
