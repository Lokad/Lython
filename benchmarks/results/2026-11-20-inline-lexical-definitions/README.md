# B1202 Shared lexical definition construction

Compared `32c5f2ef7` with `f65be4da5` on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Eligible short factories
complete defaults, function and lambda creation in their inline managed
slots. Inline and array-backed lexical calls share callable construction
and default ownership. Operations requiring a namespace or suspension use
the existing continuation with the current operands and instruction.

| Warm workload | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Eager annotated rules | −27.45% to −24.75% | −29.09% to −24.49% | 0.52% / 3.18% |
| Nested factories | −13.39% to −9.03% | −10.45% to −6.91% | 2.60% / 2.37% |
| Postponed annotated rules | −29.42% to −28.38% | −29.73% to −28.87% | 0.88% / 0.60% |
| CSV invoice control | −6.54% to +0.43% | −3.39% to +0.79% | 4.86% / 2.48% |

Ranges cross two independent process medians per revision. Each process
warmed for at least eight seconds and used five rotated rounds targeting
0.4 seconds per request. Guest elapsed includes execution, mediated
RAM-backed I/O where applicable, goldens and previous-result cleanup.
Process CPU covers the request loop. Compilation, startup, fixture reset
and final teardown are outside guest elapsed.

The factory gains repeat with the shared construction. Invoice spans
zero on both clocks. Local CLR allocation is effectively flat. Accounted
peaks are 232 bytes lower on eager and postponed rules, 24 bytes lower
on nested factories and unchanged on invoice. Shared budgets return to
baseline for all four workloads.

Both complete 32-file images passed all four workload goldens. Windows
.NET 11 and Ubuntu .NET 10 each passed 2,880 public and 1,438 white-box
focused Release checks. Coverage includes preserving the first default
while a later default suspends, alias identity, cancellation and reuse of
the borrowed allowance. Both quiet gates and all 80 timed batches passed,
workers joined and the persistent archive verified under the VM lease.

[Timing](summary.json) · [Observations](observations.tgz) ·
[Source, image identities and validation](procedures.tar.xz) ·
[Initial B1201 result](initial/README.md).
