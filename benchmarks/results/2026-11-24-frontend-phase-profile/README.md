# B1650 Frontend CPU after shared pattern binding

Three compile-only profiles use the verified `4ae7dcc38` image and complete
workload goldens from [B1648](../2026-11-24-shared-match-bindings/README.md).
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Each worker verifies its golden before and after sampling, warms
for at least eight seconds of compilation, and samples at 199 Hz for about
six seconds. Batches compile and dispose script owners. Request framing
and CLR work are also sampled. Guest execution, I/O and process startup are
outside the sampled compilation requests.

| Complete workload | Compilations | All samples | Parsing | Executable construction | Abstract interpretation | Syntax validity | Unmapped leaf |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Generated structural classifier | 16,384 | 1,214 | 355 | 369 | 91 | 106 | 330 |
| Sparse generated validator | 4,864 | 1,250 | 382 | 320 | 199 | 43 | 372 |
| Configuration manifest | 57,344 | 1,223 | 213 | 292 | 265 | 66 | 217 |

Each resolved stack contributes at most once, using its nearest Lython
frame. An unmapped leaf can still have a resolved managed caller. The full
attribution includes tokenization, lexical preparation and other frontend
work. The nearest operand layout preparation or forwarding methods account
for 68 classifier, 82 validator and 57 manifest samples. Pattern parsing
and lowering are also visible in the classifier. The manifest spends more
of its compile request in abstract interpretation. These samples locate
work and give no estimate of savings from a proposed rewrite.

The source gate for operand layout is to remove a complete instruction
walk while keeping forwarded reads, branch depths, unwind targets and
admitted operand capacity. The forward scan can rewrite earlier consumers,
so any combined pass must derive shape from settled instructions. The
compiler must also retain the original source span for a forwarded read.
Keep complete script outputs and compare all three compilation and
time-to-result lanes with the same worker controls.

Both quiet gates pass, and the workers and profiler join before fixture
removal and archive verification.

[Attribution](attribution.json) · [Image](identity.json) ·
[Raw profiles](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Procedures](procedures.tar.xz).
