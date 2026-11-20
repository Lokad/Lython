# B1178 evaluated operands through caller selection

Both executable forms now select a unary builtin before reading the operand
as an object. Ordinary helper calls keep the evaluated slot through binding.
This removes the eager box from the native call route without changing when
Python observes an identity. The reference is frozen `48c661256`, whose
production, test and worker sources match `33481a879`. The candidate is
`15cff0352`.

| Warm workload | Windows allocation change | Quiet elapsed change | Quiet process CPU change |
| --- | ---: | ---: | ---: |
| In-memory invoice helper pipeline, 4,096 lines | −24.91% | −4.43% to −2.24% | −4.31% to −0.86% |
| Eager annotated rules, 2,048 definitions | 0.00% | −3.47% to +2.82% | −3.37% to +2.94% |
| File-backed CSV invoice, 4,096 rows | 0.00% | −1.61% to +2.43% | −1.96% to +0.66% |
| Nested annotated factories, 2,048 definitions | 0.00% | — | — |

The in-memory pipeline uses one-argument pricing functions and holds the
line data in arithmetic slots. It allocates 395,080 bytes per invocation
instead of 526,152, a difference of 32 bytes per line. All four local
workloads return the shared allowance to baseline and have unchanged
accounted peaks. The two timed controls span zero, with reference elapsed
spreads of 4.35% for eager rules and 3.51% for the CSV invoice. The comparison
supports the direction of the in-memory gain. It leaves the control effects
uncertain.

On November 20, 2026 UTC, the quiet Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3. The warm guest clock includes mediated I/O where used, golden
checks and prior-result disposal. Process CPU covers the wider batch. The
ranges cross two independent worker medians per revision. The run took 188
seconds, and both quiet gates and all 60 batches passed. Workers joined and
guest fixtures were removed before the persistent archive was verified.

Both 32-file images matched four CPython goldens. Windows .NET 11 and Ubuntu
.NET 10 each passed 2,331 focused Release checks. Additional alias, numeric
slot and default tests covered synchronous and asynchronous execution and
shared-budget reuse. Retain the change. The benchmark is a Lython source-pair
diagnostic and its timing applies to the measured workloads.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).
