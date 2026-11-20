# B1179 repeated dictionary display layouts

The prototype shares the existing governed key table for repeated displays
when every evaluated string key has its original identity. Hits build only
the new row's values. Changed keys, duplicates and protocol keys use normal
dictionary construction. Each row remains independently mutable, and
expanded keywords still take an independent value snapshot before binding.

| Warm workload | Windows allocation change | Quiet elapsed change | Quiet process CPU change |
| --- | ---: | ---: | ---: |
| Expanded forwarding, 1,024 stable-key records | −31.75% | −14.24% to −13.66% | −15.38% to −12.62% |
| Expanded forwarding, first key rebuilt each row | +0.00% | +0.20% to +0.51% | −1.96% to +0.51% |
| File-backed CSV invoice, 4,096 rows | +0.00% | −0.01% to +6.37% | 0.00% to +6.12% |
| Eager annotated rules, 2,048 definitions | 0.00% | — | — |

The stable-key forwarding script moves from 2,423,918 to 1,654,288 managed
bytes per warm invocation. Its accounted peak moves from 4,809,754 to
2,976,186 bytes. All four local workloads restore the borrowed allowance,
and the other three accounted peaks hold within 288 bytes.

The first run measures `289380214` against the frozen `15cff0352`, whose
production, test and worker sources match `9cb7532fc`. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20, 2026 UTC. The
warm guest clock includes mediated I/O, golden checks and prior-result
disposal. Reset, startup, compilation and final teardown are outside that
clock. Process CPU covers the wider batch. The ranges cross two independent
worker medians per revision.

Expanded forwarding's duplicate elapsed spreads are 0.35% and 0.33%. The
first CSV run has a 4.79% candidate spread and several adverse pairings.
The original 60 batches remain in the record. Both quiet gates passed,
workers joined and guest fixtures were removed before the persistent
archive was verified under lease.

An invoice-only confirmation used the exact same images, fixture and
timing boundary. Elapsed changed by −1.62% to +0.48% and CPU by −1.33% to
+1.33%. Its candidate elapsed spread fell to 0.66%. The initial adverse
direction did not repeat. All 20 confirmation batches passed with quiet
gates, worker joins, fixture cleanup and a verified archive.

Each 32-file image matched four CPython goldens. Windows .NET 11 and Ubuntu
.NET 10 each passed 838 focused Release checks. Coverage includes key identity,
duplicate precedence, independent mutation, keyword snapshots, cancellation
during admission and budget reuse. Retain the shared layout for the stable
key workload and keep both CSV observations.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).
