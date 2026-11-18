# B1008 retained rule-closure attribution

The quiet Ubuntu Azure F4as_v7 profiled `0547e5489` with core hash prefix
`62665014` on 2026-11-18 UTC. The exact B1007 scripts and full worker
image were verified against their recorded hashes. Both scripts warmed for
eight seconds before five seconds of whole-batch CPU sampling. Both quiet
gates passed and all workers joined before archived traces were verified
and RAM fixtures removed. [B1007 comparison](../2026-11-18-function-annotation-inputs/README.md).

| Script | Resolved runtime samples | Closure capture or retention | Function metadata | Lowered expression evaluation |
| --- | ---: | ---: | ---: | ---: |
| Rule closures | 1,093 | 25 | 0 | 0 |
| Annotated rule closures | 1,008 | 16 | 422 | 109 |

The ancestry groups overlap. Inclusion in a stack does not estimate the
cost of removing its named method. The unannotated script also had 325
samples with prepared definition execution and 283 with native or borrowed
invocation. The annotated script had 652 with prepared definition execution
and 168 with native or borrowed invocation. Perf maps and the whole-batch
sampling boundary make this diagnostic attribution.

The profile provides little evidence for separating captured cells from their
invocation context. That redesign would also need to preserve the transition
to live namespaces and writes through captured cells. The more concentrated
candidate is preparing ordinary annotation operands in executable code and
building the fresh exact annotation dictionary directly. Module and class
annotation mappings still require Python subscript semantics.

[Attribution](attribution.json) · [Raw traces and executed scripts](observations.tgz) ·
[Procedure and classifier](procedures.tar.xz) · [Terminal state](terminal-status.json)
