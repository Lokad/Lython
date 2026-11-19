# B1027 runtime attribution for rule definitions

Profiled retained runtime `b8532e0ef / 79d43441` on the quiet Ubuntu Azure
F4as_v7 with .NET 11 RC3 on 2026-11-19 UTC. The rule sources and fixtures
match B1022. The complete 32-file worker image verified with CPython goldens.
Each workload warmed for eight guest seconds before about five seconds
of whole-batch CPU profiling.

| Workload | Resolved runtime stacks | Prepared definition | Default construction or lookup | Function metadata | Reserve nearest runtime frame |
| --- | ---: | ---: | ---: | ---: | ---: |
| Unannotated rules | 1,058 | 346 | 98 | 1 | 123 |
| Annotated rules | 1,008 | 349 | 44 | 56 | 109 |

The default-construction owner is the nearest caller of reserve in 43
unannotated and 32 annotated samples. Positional default completion accounts
for 15 more in the unannotated script. The enclosing function constructor
accounts for 12 and 33, respectively. Groups overlap, and sampled ancestry
includes useful work. Results guide source inspection and cannot predict
how much time a redesign will remove.

Definition construction remains worth investigating after the direct
completion change. Repeated keyword-default and annotation records still
build mutable values, key backing and hash tables each time. The prototype
reuses the existing immutable record-key layout and fills private value
storage for each definition. The plan checks mutation order, alias
lifetime, reentry, refusal and cleanup alongside both rule workloads.

Profiling uses the existing worker with managed symbol maps. The whole
batch includes setup work around the guest elapsed boundary. Both quiet
gates and output checks passed. All workers joined, and trace bytes were
archived and verified under the lease before fixture removal. Unknown and
ambiguous samples remain in the archived evidence.

[Attribution](attribution.json) · [Raw profiles](observations.tgz) · [Procedures](procedures.tar.xz).
