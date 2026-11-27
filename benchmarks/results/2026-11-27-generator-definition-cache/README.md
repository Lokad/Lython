# B1897 Share lookup storage across generator calls

An ordinary Python function already admits its lookup table once and
shares it across calls. A generator function previously admitted and
filled a separate table in every instance. The candidate uses the same
definition-owned table for both kinds of function. The table is allocated
on first use, and interleaved generator frames keep separate locals,
operand storage and suspension state. Record entries remember layout
and key, and every load reads the current value from the current row.

The reference uses `4f0f9e44f`, whose production source is the
`36ed51e1b` image measured in B1896. The two frozen 32-file images differ
only in the core DLL. Both verify the four independent CPython 3.13.16
goldens. The workload reads 2,048 CSV records from 27,350 bytes and
creates a four-yield generator for each row. The allocation witness on
Windows .NET 11 shows 245,544 fewer CLR bytes per synchronous run
(9.28%) and 245,774 fewer per asynchronous run (9.29%). Admission peaks
match and the allowance returns to baseline in both modes and all four
fixtures. [B1896 CPython comparison](../2026-11-27-current-quiet-cpython/README.md).

## Timing

On November 27, 2026 UTC, the quiet Azure F4as_v7 runs Ubuntu 24.04.4 and
.NET 11 RC3. The preliminary run uses Ubuntu WSL on an Intel i7-14700KF,
pinned to CPU 27, with the same images. Each image has two independently
warmed workers and seven rotated batches per worker and lane. Ranges
compare all pairings of worker medians. Elapsed includes mediated
RAM-backed reads, golden verification and previous-result disposal.
Process CPU includes fixture reset and request framing as well.

| Warm script and hosting mode | WSL elapsed change | WSL CPU change | Quiet VM elapsed change | Quiet VM CPU change |
| --- | ---: | ---: | ---: | ---: |
| Iterable-row `starmap`, `Run` | −9.25% to −6.21% | −9.24% to −3.78% | −10.86% to −7.21% | −10.99% to −6.28% |
| Iterable-row `starmap`, `RunAsync` | −7.99% to −5.16% | −8.97% to −4.38% | −8.30% to −6.45% | −7.96% to −5.32% |
| Bound `starmap`, `RunAsync` | −1.89% to −1.37% | −3.80% to 0.00% | +1.80% to +3.45% | +2.15% to +3.92% |
| File-fed quantity map, `Run` | −4.74% to +2.42% | −1.28% to +1.28% | −2.17% to +1.18% | −0.99% to +1.00% |

Retain the shared ownership and measured improvement in the iterable
report. The adverse bound result on the VM did not appear on WSL.
Its cause is unresolved. Retain that result for the next comparison
on the ordinary bound-call path. Both file-fed controls span zero.
All 224 timed batches verify. The VM passed both quiet gates, joined
all workers, removed fixtures and verified its persistent archive.

## Correctness

Each Windows and Ubuntu WSL Release target runs 107 white-box checks and
170 public checks. The new cases cover interleaved record layouts at
different positions, live mutation, first-admission refusal, cancellation
and budget reuse. Focused checks also cover suspension and iterator cleanup.
The initial test fixture tried to observe unmirrored module locals via the
namespace dictionary. The passing fixture returns both instances through
the executable path. The original logs are in the procedure archive.

[Quiet timing](summary.json) · [WSL timing](local-summary.json) ·
[Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Quiet observations](observations.tar.xz) ·
[Local observations](local-observations.tar.xz) · [Receipts and tests](procedures.tar.xz).
