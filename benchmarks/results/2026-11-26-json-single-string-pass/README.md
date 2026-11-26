# B1796 Validate raw UTF-8 during the JSON string scan

The candidate on `5c0fbe556` uses bounded ASCII runs to check syntax and
encoding together. Non-ASCII bytes use the managed rune decoder. A rune
can cross the cancellation-window boundary without changing its decoded
value. Invalid UTF-8 remains recorded until the lexer checks syntax and
lone surrogates, and is rejected before admitting the result. The pass
replaces the final full-token UTF-8 validation. Escape sizing and output
construction keep their existing admission and ownership.

Both 32-file images match five independent CPython output and file
goldens. The new raw Unicode report uses the same 3,072 records and
expected file as the escaped report, with UTF-8 values in the input.
Windows .NET 11 and Ubuntu .NET 10 pass 978 public and 229 white-box
focused Release checks each. CPython 3.13.2 matches both valid
rune-boundary cases. The white-box suite also verifies malformed
internal bytes at the boundary and refusal before output allocation.
Mediated I/O, expected outputs and budgets are verified before the
source-pair timing.

The local Ubuntu WSL diagnostic uses two workers per source and seven
rotated batches per worker. Ranges compare all four pairs of worker
medians. Elapsed includes applicable mediated I/O, output checks and
previous-result disposal. Process CPU also covers fixture reset and
request framing. Compilation and startup sit outside these intervals.
Negative values indicate less time.

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| JSON service totals | Sync | −5.56% to −1.72% | −3.03% to +0.94% |
| Raw Unicode JSON report | Sync | +0.79% to +4.57% | −1.76% to +5.75% |
| JSON Lines helper | Async | −2.35% to +7.32% | −6.49% to +5.73% |
| Unicode text control | Sync | +0.48% to +6.86% | +0.77% to +4.45% |

Keep the candidate out of production. The local Windows allocation
diagnostic finds identical allocation for service totals and JSON Lines,
and identical peaks across all five cases. The raw Unicode report takes
longer locally and in the quiet confirmation. In the quiet run its CPU
rises 2.22–4.55%, while service totals and Lines CPU span zero.


All 112 timed batches complete, workers join and guest fixtures are
removed. The persistent observations archive verifies. This is a local
diagnostic with same-source controls. The original R201 search trial
is a separate implementation and retains its adverse observations.
[Earlier R201 trial](../2026-11-10-json-string-scan/README.md).

[Images](identity.json) · [Timing and spreads](summary.json) ·
[Allocation and admission](allocation-summary.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Procedures and exact patch](procedures.tar.xz).

[Quiet VM confirmation](quiet/README.md).
