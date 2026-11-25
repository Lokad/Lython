# B1690 Conditional boolean precedence descent

The private parser starts at a `not` operand and enters a tighter boolean
layer only for the right side of `or`. It removes the old shared dispatcher
and the two boolean entries in the arithmetic table, for 22 net production
lines removed. Independent `and` and `or` chain limits, error anchors, AST
order and cancellation stay in place.

The source pair consists of `e4bbc3925` and the archived parser patch. The
quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 on November 25, 2026 UTC.
Two workers per source each contribute seven rotated batches. Ranges span
all pairings of worker medians. Negative values mean less time. Compile
includes disposal. Through result adds execution, mediated RAM-backed I/O,
output and file verification and result cleanup. The warm invoice reuses
compiled code. The mixed variant normalizes the same file as the sparse
validator with 96 conditions that combine `or` and `and`. Its output is
verified independently.

| Workload and boundary | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Sparse guards, compile | −2.34% to +0.24% | −1.45% to −0.33% |
| Mixed boolean guards, compile | −1.05% to +0.84% | −1.48% to +0.30% |
| Configuration manifest, compile | −3.35% to +0.78% | −4.51% to +1.26% |
| Sparse guards, through result | −0.85% to +0.28% | −1.95% to −0.50% |
| Ordinary definitions, compile | −0.28% to +1.06% | −2.82% to +0.49% |
| Mixed boolean guards, through result | −0.32% to +1.48% | +0.67% to +2.30% |
| Configuration manifest, through result | −1.01% to +0.75% | −2.22% to +1.00% |
| Quoted invoice control, warm | −0.77% to +3.13% | +0.00% to +2.20% |

Keep this candidate private. Sparse guards use less process CPU, while
mixed guards through result use more. Elapsed spans zero throughout and
the invoice CPU range is nonnegative. A single-pass design that also
removes the remaining `or` operand recursion would justify a changed-source
follow-up. The current timing does not isolate the source of the adverse
points.

Both images match six complete CPython 3.13.2 goldens. All 120 executable
bodies, 8,682 instruction spans, compile admission and runtime peaks
match. Warm allocation matches on five cases and varies by 48 B on the
invoice. The Windows .NET 11 and Ubuntu .NET 10 public suites each pass
772 focused cases and the Ubuntu white-box suite passes 45. The two
quiet gates, all 224 timed batches, worker joins, temporary fixture
cleanup and persistent archive verification pass.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Procedures and patches](procedures.tar.xz) ·
[Terminal status](quiet-terminal-status.json).
