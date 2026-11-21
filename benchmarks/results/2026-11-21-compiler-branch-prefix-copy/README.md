# Compiler branch-prefix copy

The prototype uses the existing stack layout scratch to record the first
logical branch in each block. It then copies the preceding instructions
in bulk. It removes per-instruction copy and opcode dispatch in that
prefix, while the stack validation and final tape traversal remain.

The local Windows .NET 11 Release diagnostic compared production
source `256c5e651` with `9c2b6e46d`. Both complete 32-file images
passed all three workload goldens. Each diagnostic compares the
prototype with the reference or the reference with an identical
reference process. Compilation includes code-owner disposal. Time
to result also includes mediated fixture I/O, verification and
prior-result cleanup. Positive elapsed change means slower.

| Complete script | Boundary | Prototype elapsed change | Identical-image control |
| --- | --- | ---: | ---: |
| Generated validator | Compile | +6.72% | +5.40% |
| Generated validator | Time to result | +12.39% | −8.65% |
| Postponed nested factories | Compile | +0.32% | +2.01% |
| Inherited dataclasses | Compile | +0.26% | +1.37% |

The validator time-to-result observations have IQR-to-median fractions
above one for both Lython workers in each comparison. These local
observations establish no compile or whole-result improvement.
Local compile allocation matches on factories and dataclasses.
Validator median allocation is about 5 KB higher per compilation out
of 2.17 MB. The diagnostic verifies that all compiled owners refund.

The first prototype passes 1,061 white-box and 647 public focused
tests. The 57 code objects in the three benchmark fixtures have
identical instruction tapes, operand error spans and exception
regions. A separate assertion in a protected suite then exposes
one branch whose target remained a logical block ID after the first
prototype's bulk copy. It follows a terminal assertion failure and
was still retargeted by the reference. Corrected private revision
`798c97274` now matches all 59 code objects, including this case.
The timing table describes the original prototype.

Keep the work private. The copy change leaves the full storage
stage and its intermediate representation in place. The measurements
do not justify qualification or a speedup claim. A different design
that removes that stage remains open.

[Timing summaries](summary.json) ·
[Source, checks and original observations](source-and-observations.tar.xz).
