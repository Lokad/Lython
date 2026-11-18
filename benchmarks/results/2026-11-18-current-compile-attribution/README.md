# B1013 and B1014 public-compile attribution

Profiled prototype `9c60eed7f` / `df78258f` on the quiet Ubuntu Azure
F4as_v7 on 2026-11-18 UTC. These are the exact B1012 candidate runtime
and worker files. The manifest and generated validator preserve the B804
source and goldens. The complete 32-file image and both cases verified
against the actual workers. The generated case is a compilation stress
shape alongside the representative manifest.

B1013 stopped after 128 batches of 256 manifest compilations accumulated
7.87 seconds of guest warmup. The required warmup was eight seconds.
It captured no sampling window. Worker exit, the quiet checks and the
verified archive completed. [Original run](initial/terminal-status.json).

B1014 uses batches of 1,024 manifest and 256 generated compilations,
with the same warmup target, batch bound and five-second capture. It
completes both profiles and validates the output and source checks.

| Nearest phase in sampled stacks | Manifest, 1,022 samples | Generated validator, 1,062 samples |
| --- | ---: | ---: |
| Static and annotation analysis | 349 | 256 |
| Parsing and tokenization | 258 | 419 |
| Executable preparation | 232 | 282 |
| Scope and syntax facts | 125 | 73 |
| Lowering | 38 | 25 |
| Other CLR or protocol | 20 | 7 |

Nearest owning frames include useful and inlined work. Counts do not
forecast savings or compare elapsed time. Unmapped leaf counts were 162
and 316. In the manifest, 41 nearest phase frames are the abstract fact
query and 24 are dictionary fact invalidation. Both repeatedly inspect
known value shapes. Investigate computing facts as values are produced
and reducing the whole invalidation pass, with exact checks on mutable
aliases, retained scopes, annotation flow, error ordering, admission
and cancellation. The source gate needs a precise snapshot ownership
proof before any graph walk can be removed.

The generated validator attributes more CPU to parsing. No repeated
parsing stage has been demonstrated there. The phase classification
uses method owners with signature parameters removed. Both successful
quiet gates, all joined workers and verified archive before RAM removal
are recorded. Profiling clocks cover a wider boundary than compiler
execution and cleanup. No CPython timing was collected here.

[Attribution](attribution.json) · [Confirmed raw archive](observations.tgz) ·
[Initial raw archive](initial/observations.tgz) · [Procedures and checks](checks-and-procedures.tar.xz)
