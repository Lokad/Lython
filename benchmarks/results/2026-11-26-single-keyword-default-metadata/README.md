# B1797 Store a singleton keyword initializer directly

The candidate on `5c0fbe556` stores a single immutable keyword literal
in its existing definition metadata. It removes the singleton tuple
and its backing array from compilation. Multiple literals still use a
tuple. Each function receives its own admitted default record. The
Windows .NET 11 diagnostic measures 80 B less compiler admission for
the escaping-rule script. Its warm CLR allocation and peak admission
match, and both allowances return to baseline.

Both 32-file images match four independent CPython goldens. Windows
.NET 11 and Ubuntu .NET 10 each pass 1,036 public and 259 white-box
focused Release tests. The first private snapshot lacked the 20
representative scripts needed by 14 tests. Restoring the tracked
fixtures from the base revision resolves those failures on each target,
and the procedures archive retains the initial logs.

The local Ubuntu WSL diagnostic uses two workers per source and seven
rotated batches per worker. The ranges cover all pairs of worker
medians. Warm elapsed includes output checks and previous-result
disposal and applicable mediated I/O in the file-search control.
The compilation lane measures compile and compiled-program disposal.
Process CPU also covers request work and fixture reset where applicable.
Negative values indicate less time.

| Workload | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Escaping rules, compile | −0.13% to +3.16% | +1.06% to +5.13% |
| Ordinary definitions, compile | −3.32% to +1.82% | −6.37% to −0.79% |
| Protected file-search control, warm sync | 0.00% to +1.73% | −2.74% to +5.92% |
| Escaping rules, warm async | −6.36% to −0.31% | +0.00% to +5.09% |

Keep the prototype private. The local compile-to-program result for
escaping rules has mixed elapsed and 1.06–5.13% higher process CPU.
Warm async elapsed falls while its CPU does not. The unchanged file
search spans zero in CPU. Saving 80 B of compiler admission does not
resolve the compilation cost or justify a VM repeat on this design.

All 112 batches complete. Workers join and guest fixtures are removed.
The observations archive verifies. These are local source-pair timings.

[Images](identity.json) · [Timing and worker spreads](summary.json) ·
[Allocation and admission](allocation-summary.json) ·
[Observations](observations.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Procedures and exact patch](procedures.tar.xz).
