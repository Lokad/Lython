# B1420 Borrowed short UTF-8 dictionary keys

The nonlocal report attributed 161 of 1,092 profile samples to its
dictionary loop. This change skips query hashing and bucket traversal
when that loop or canonical record-name lookup searches for a key of
at most eight UTF-8 bytes in a native table with at most four entries.
It reuses existing entries and the normal mutation path. Larger tables
and keys, non-text keys and Python-defined protocols keep their existing
paths. The change adds no cached state, storage or reservation.

Compared production source `6ac96ea93` with `a0382d044e`, retained as
`ae8377368`. The complete 32-file images differ only in the core library.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 22, 2026 UTC.

| Complete script | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nonlocal Unicode report, 3,072 lines | Warm | −8.17 to −6.58% | −8.09 to −5.87% |
| Nonlocal Unicode report, 3,072 lines | Compile to result | −4.72 to −2.97% | −6.15 to −3.54% |
| Ordinary histogram | Warm | +0.37 to +0.66% | −1.10 to −0.39% |
| Dense Unicode line | Warm | −22.67 to −14.66% | −18.85 to −11.60% |
| Nonlocal report, 514 distinct keys | Warm | +0.70 to +1.87% | −0.41 to +1.59% |
| JSON Lines export with expanded options | Warm | −1.89 to −0.01% | −0.16 to +0.16% |
| Quote-heavy CSV invoice | Warm | +0.07 to +1.56% | 0.00 to +1.15% |
| Fully supplied helper | Warm | −3.01 to 0.00% | −3.40 to +0.72% |

The ranges span all pairings of the medians from two independent workers
per source. Each worker warms for eight seconds and runs seven rotated
batches per lane. Warm elapsed includes mediated RAM-backed I/O, output
verification and prior-result disposal. Compile to result adds compilation
and compiled-owner cleanup. Process CPU also covers resets and framing.
Startup and final teardown sit outside the elapsed clocks. The small
positive elapsed observations in the wide-vocabulary and invoice controls
bound the observed tradeoff. The dense line's same-source workers spread
by 4.69% and 5.41%, with the elapsed and CPU directions holding across
the worker pairings. No external timing ratio was refreshed.

Companion Windows measurements find effectively flat managed allocation
and matching admitted execution peaks. The shared allowance returns to
baseline. Both images match the same seven CPython goldens, including
the 514-key output's exact file contents. Windows .NET 11 and Ubuntu
.NET 10 each pass 393 white-box and 1,490 public Release checks. Four
initial public failures arose from missing CSV scripts in the sparse
test worktree. All four pass after restoring those fixtures. The
archive retains the original logs and targeted reruns.

Both quiet gates and all 224 timed batches pass. Workers join, guest
fixtures are removed and the observations archive verifies under the
lease. B1419 holds the exact reference core.

[Sources and image hashes](source.json) · [Timing and worker spread](summary.json) ·
[Allocation and admission](local-allocation.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Profile](../2026-11-22-retained-runtime-profile/README.md) ·
[B1419 reference](../2026-11-22-callable-checkpoint-cpython/README.md).
