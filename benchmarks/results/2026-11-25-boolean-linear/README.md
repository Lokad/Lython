# B1693 Single-pass boolean expression parsing

One loop parses conjunctions and disjunctions. It closes the current
conjunction on reaching `or` and builds the same binary expressions with
independent chain limits and the original diagnostic anchors. This removes
recursive boolean precedence descent and keeps the arithmetic operator
table specific to arithmetic. The conditional-descent prototype remains
in the record as [B1690](../2026-11-25-boolean-precedence/README.md).

The source pair is `f1c4f33ba` and the archived production patch. The run
used the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Two workers per source each contributed seven rotated batches
per lane. The ranges span the four pairings of worker medians. Compile
includes disposal of compiled storage. Through result adds execution,
mediated RAM-backed I/O, output and file verification, and result cleanup.
The warm invoice runs the already compiled script.

| Workload and boundary | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Sparse guards, compile | −2.65% to −0.02% | −3.49% to +0.92% |
| Mixed guards, compile | −0.44% to +1.00% | −1.51% to −0.62% |
| Configuration manifest, compile | −1.32% to −0.31% | −2.17% to −0.59% |
| Sparse guards, through result | −2.98% to −1.00% | −4.59% to −0.24% |
| Ordinary definitions, compile | −0.72% to +1.01% | −0.20% to +0.90% |
| Mixed guards, through result | −1.16% to +0.86% | −0.75% to +0.65% |
| Configuration manifest, through result | −1.33% to +0.42% | −2.15% to +0.23% |
| Quoted invoice control, warm | −0.99% to +0.17% | −1.67% to +0.00% |

Retain the single-pass implementation for the simpler grammar path and
the modest sparse-guard improvement. Mixed-guard time through result and
the invoice control remain inconclusive. The earlier mixed-guard CPU
increase did not recur on this revised patch. Same-source elapsed spreads
reach 1.52%, so small changes merit caution.

Six complete CPython 3.13.2 goldens, 120 executable bodies and 8,682
instruction spans match. Retained compile admission and runtime peaks
match. Warm allocation matches on four cases and falls 120 B each on two.
Windows .NET 11 and Ubuntu .NET 10 each pass 773 focused public cases, and
Ubuntu passes 45 focused white-box cases. The diagnostic regression also
passes on the frozen reference. Both quiet gates, all 224 batches, worker
joins, fixture teardown and archive verification pass.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Procedures and patches](procedures.tar.xz) ·
[Terminal status](quiet-terminal-status.json).
