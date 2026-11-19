# B1075 combined prepared definition dispatch

Private `0cbe8f867` folds prepared definition operations into the existing
operation dispatcher. It removes the second type selection and leaves
instruction encoding and suspension unchanged. The reference is retained
`773be8dc2`, including direct capture access.

The original prototype passed 630 focused Release cases per target. After
rebasing, the exact Windows .NET 11 and WSL .NET 10 candidate passed 297
cases per target, covering default evaluation, annotations, ownership and
nonlocal behavior. The initial post-rebase freeze rejected the earlier image.
The new complete 32-file images passed all five CPython goldens. Runtime
allocation and the accounted peak match across the pair.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover all four combinations of duplicate process medians. Negative
values favor the prototype.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rules | Warm | −0.16…+5.52% | −3.05…+4.68% |
| Ordinary rules | Compile-to-result | −1.18…+1.91% | −3.37…+2.00% |
| Annotated rules | Warm | −2.46…+0.64% | +0.05…+1.57% |
| Retained siblings | Warm | −3.52…+0.39% | −2.39…+2.04% |
| Captured rules | Warm | −2.89…−0.06% | −2.52…−0.62% |
| Expanded forwarding | Compile-to-result | −0.61…+0.23% | −0.70…+1.32% |

The ordinary warm candidate controls differ by 5.03%. The captured reference
spread is 2.54% and annotated CPU increases in all pairs. Confirm ordinary,
annotated and captured warm execution with a longer warmup and the same
forwarding control before selecting the dispatch change.

Both quiet gates and all 120 timed goldens passed in 274.27 seconds. Workers
joined, guest fixtures were removed and the archive verified under the lease.
Warm includes mediated I/O, goldens and previous-result cleanup. Compile-to-
result adds compilation and compiled-owner disposal. Process CPU observes a
wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
