# B1816 Short file-script CPU attribution

One current Lython source, `c10f06884`, runs the first-line search and a
short configuration edit on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3 on November 27, 2026 UTC. These are active CPU samples at
199 Hz over six seconds per worker after eight execution-clock seconds
of warmup. Each capture includes fixture reset, output verification and
request framing outside the guest execution path. The ordinary comparison
clock excludes fixture reset and includes verification.

| Attribution | First-line sync | Config edit sync | First-line async |
| --- | ---: | ---: | ---: |
| Verified invocations | 46,080 | 52,224 | 44,544 |
| All samples | 1,167 | 1,130 | 1,264 |
| With acquisition on the stack | 285 | 270 | 257 |
| Of those, unresolved kernel leaf | 237 | 225 | 225 |
| With file read or write transport | 32 | 30 | 29 |
| With worker scheduling or completion | 60 | 40 | 45 |
| With builtin table creation | 5 | 7 | 12 |
| With fixture reset | 505 | 371 | 468 |
| With file-output verification | 151 | 173 | 162 |

Ancestry groups can overlap. Unresolved kernel leaves are attributed to
an observed ancestor. Acquisition includes the mediated host's file open
and filesystem checks, so its count gives no estimate of removable
interpreter work. The capture gives no reason to change stack workers or
builtin initialization for this gap. A separate inside/outside verification
clock would help characterize the fixed-cost gap while keeping the existing
command comparison and full parity checks. [B1815](../2026-11-27-compiled-prefix-cpython/README.md).

The two independent CPython output and file goldens, 32-file image,
quiet gates and archive hash verify. The prior local thread-time trace
included idle workers and prompted this active-CPU capture.

[Attribution](attribution.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Verification and procedures](procedures.tar.xz).
