# B1300 quiet confirmation of inline regex scratch

This uses the same `d51891009` and `168cdf7a4` images as
[B1299](../README.md), now on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3. Both use the optional 0.4.0 provider. Two independently
warmed workers per image supply seven rotating timed batches each.
Ranges span the four pairings of process medians. Warm elapsed
includes execution, golden checks and prior-result disposal.

| Complete warm workload | Elapsed change | Wider process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Single match on each of 960 log records | −3.68% to −0.58% | −5.31% to −1.28% | 0.86% / 2.34% |
| Cursor on the same data | −1.41% to +0.31% | −1.27% to +0.81% | 1.24% / 0.49% |
| Wide single matches, 13 capture slots | −0.34% to +2.70% | +0.32% to +2.85% | 1.23% / 1.80% |

Small matches use less CPU and allocated 3.61% less in the local
diagnostic. The cursor control spans zero. Wide elapsed spans zero
and CPU rises in all four pairings. Keep this version private.
The refinement should retire the one-shot preparation wrapper at
all widths and keep a single validation and publication sequence.

Both complete 32-file images matched all three CPython goldens.
All 84 timed batches and both quiet gates passed. The workers joined,
guest fixtures closed and the durable archive verified under the lease.
The candidate also passes 489 focused Release checks on each of
Windows .NET 11 and Ubuntu .NET 10.

[Timing summary](summary.json) · [Raw observations](observations.tgz)
· [Source and checks](checks-and-procedures.tar.xz).
