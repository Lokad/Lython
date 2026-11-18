# Helper normalization and protected-search CPU attribution

B987 profiled exact `bfe076110` / frozen `610d403d` on the quiet Azure
F4as_v7 VM on 2026-11-18 UTC. Both complete scripts passed verification
against existing CPython goldens. The verified 32-file .NET 11 RC3 image
used normal GC and tiering, with managed perf maps for attribution.
Guest files lived on RAM-backed storage through the complete capture.

| Capture | Helper normalization | Protected file search |
| --- | ---: | ---: |
| Guest elapsed | 4.733 s | 4.194 s |
| All-thread CPU samples | 1,003 | 991 |
| Resolved runtime ancestry | 914 | 759 |
| Unmapped leaves | 206 | 347 |
| Fixture work outside resolved runtime ancestry | 70 | 215 |
| Inclusive native calls within runtime | 478 | 264 |
| Inclusive host I/O within runtime | 78 | 92 |

The helper has 91 inclusive right-trim, 64 join/drain and 22 split-building
stacks. Protected search has 60 nearest `StartNativeCall` and 73 nearest
`OpenedByteFile.OpenAsync` stacks, counting only resolved runtime samples.
Inclusive groups overlap. Inlining and unmapped leaves obscure finer
attribution. These counts establish neither an exclusive elapsed share nor
an unnecessary call frame. Retain the current call design and stack guards.
The earlier slower shared call-control and result-carrier experiments remain
relevant. [Prior call design](../2026-11-16-complete-call-design/README.md)

Source confirms a drain-specific deletion candidate. An exact list already
owns its backing and cannot invoke guest iteration protocols. The existing
private drain can copy it into admitted capacity in bounded managed chunks.
It must keep the snapshot across output admission and observe any reentrant
mutation before the copy starts. Growth beyond capacity keeps the existing
live cursor. The next focused prototype tests this boundary independently of
shared frame or compiler work. This profile makes no speedup claim.

Workers warmed for eight guest seconds and used approximately five seconds
of wall capture at 199 Hz. Both quiet endpoints passed and all users joined.
The complete RAM traces were archived and compared byte-for-byte before
unmount. Only two exact committed raw-observation replicas were retired, with
fresh path, owner, hash and alias checks.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Image identity and procedures](procedures.tar.xz)
