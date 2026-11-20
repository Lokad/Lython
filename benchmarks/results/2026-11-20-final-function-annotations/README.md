# B1185 direct storage for function annotations

Private `418068183` shares a single unpublished definition owner for
defaults and annotations. Executable definition operands write to the
final value slots. This removes the staged annotation operands and their
final copy, and uses prepared positions for positional and keyword
defaults. Callable and annotation storage are admitted together after
decorator expressions and before defaults and eager annotations. Tight
budgets can refuse earlier.

| Workload | Boundary | Windows warm allocation | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Eager annotated rules | Warm | −1.57% | +1.32% to +5.04% | +2.69% to +3.76% |
| Nested annotated factories | Warm | −1.10% | −0.60% to +2.27% | −2.72% to +0.35% |
| Every annotation dictionary read | Warm | −1.25% | −0.16% to +3.72% | +0.62% to +3.62% |
| Eager annotated rules | Compile-to-result | — | +4.90% to +6.11% | +4.70% to +6.86% |
| File-backed CSV invoice | Warm | −0.003% | −2.73% to +0.41% | −3.26% to +1.30% |
| Unannotated rules | Warm | −1.99% | — | — |

The allocation diagnostic is separate. Accounted peaks stayed flat or
fell by 24 bytes per run. Each workload restored the shared allowance to
baseline. The first prototype, `02d62fe5c`, added 16 managed bytes to a
definition owner and allocated 2.16–3.97% more on the four factory cases.
Its source, binary and allocation results are archived under `initial/`.
The refined owner derives its annotation reservation from capacity and
uses compiler-prepared destinations in place of a retained layout and
positional cursor.

Keep this design private. Eager warm and compile-to-result CPU increased
across all pairings. CPU also increased when every annotation dictionary
was read. The nested and invoice timing ranges span zero. A successor
should eliminate dispatch or publication per annotation, in addition to
the final copy. The comparison does not attribute the measured cost to a
specific runtime method or instruction.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. Reference image `848cbf783` matches `8fafe1a5a` in tracked
core, adapter, test and worker sources. Warm covers mediated I/O where used,
golden checks and prior-result cleanup. Compile-to-result adds compilation
and its owner cleanup. Process CPU covers the wider batch. Ranges cross
two process medians for each revision.

Windows .NET 11 and Ubuntu .NET 10 each passed 521 focused Release checks.
Both complete images matched five CPython goldens and six additional
probes in both Lython modes. Coverage includes callback order, suspended
definition cleanup, early admission refusal and escaped decorator aliases.
Two earlier probes used return annotations that the Lython frontend
rejects. The original output and corrected fixtures are in the archive.
Both quiet gates and all 100 timed batches passed. The archive was verified
under the lease after workers joined and fixtures were removed.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).
