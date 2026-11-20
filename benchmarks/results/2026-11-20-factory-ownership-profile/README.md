# B1170 factory ownership and dispatch attribution

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20
UTC. The exact `02639bec8` image has the same runtime and worker sources
as `9b14997e4`. All 32 files and both CPython goldens verified. Each
worker ran at least eight seconds of warmup and verified output before
and after five seconds of sampling.

| Sampled stacks | Eager rules | Nested factories |
| --- | ---: | ---: |
| Identifiable Lython runtime ancestry | 1,002 | 1,002 |
| Nearest runtime frame is the instruction prefix | 215 | 198 |
| Nearest runtime frame is result completion | 66 | 63 |
| Nearest runtime frame is memory reservation | 72 | 70 |
| Positional default storage calls that reservation | 25 | 17 |
| Prepared keyword record calls that reservation | 20 | 11 |
| Callable construction calls that reservation | 22 | 27 |
| Capture resolution or transfer in ancestry | 9 | 16 |
| Generic binder in ancestry | 13 | 16 |
| Scope promotion in ancestry | 0 | 0 |

Counts can miss inlined and unresolved frames. Ancestry groups overlap.
The captures cover whole warm requests, with output checks and result
cleanup. Both quiet gates passed. Workers joined and the trace archive
was verified in persistent storage before the RAM mount was removed.

The positional tuple and keyword dictionary currently preflight separately
even when both initial sizes are known. A single preflight can remove one
resize and the partial-construction rollback. Test it through the existing
default owner, preserving prior admission of both containers, source-ordered
expressions, live default aliases and cancel/refund behavior. Keep allocation,
accounted peak and paired CPU and elapsed distinct. These samples do not
measure a speedup.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Images and procedure](procedures.tar.xz) ·
[Exact measured core](../2026-11-20-funded-definition-construction/measured-core.dll.gz).
