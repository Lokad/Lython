# B1262 factory and validator CPU attribution

Profiled `31abfeb3c` on November 21, 2026 UTC on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Independent workers warmed
for at least eight seconds before sampling. The warm profiles cover
complete batches, including mediated RAM-backed I/O, verification and
prior-result cleanup. The compilation profile excludes guest execution.

| Samples or nearest resolved method | Postponed factories, warm | Validator, warm | Validator, compile |
| --- | ---: | ---: | ---: |
| All process samples | 1,420 | 1,334 | 1,835 |
| Unresolved leaf | 91 | 814 | 533 |
| Native or general instruction loop | 306 | 87 | — |
| Memory reservation | 208 | 46 | — |
| Byte file acquisition | — | 168 | — |
| JSON construction | — | 42 | — |
| Native dictionary storage replacement | — | 19 | — |
| Frontend compile coordinator | — | — | 1,247 |
| Executable code-object building | — | — | 408 |
| Lexical scope preparation | — | — | 159 |

The factories create 4,096 helpers and report the final annotation
keys. Resolved reserve callers include callable creation (92), keyword
default storage (42), positional default storage (36) and cell capture
(29). These are live allocations. The inclusive call counts also cover
executed bodies, so they cannot predict the benefit of a frame redesign.
Earlier combined admission and deferred tuple designs remain adverse
on some representative controls. [Combined default admission](../2026-11-20-default-container-admission/README.md)
· [Deferred positional tuples](../2026-11-20-lazy-positional-defaults/README.md).

The validator converts and normalizes 96 fields with successful integer
parses. Its warm profile shows file acquisition and fixture work
alongside runtime construction. Its compile profile identifies lexical
preparation as a bounded target. Syntax validation already visits
expressions. Reusing that walk to establish whether lexical expression
facts are needed can remove subsequent binding and read walks on
ordinary scripts. The sample counts provide no speedup estimate.

Both CPython goldens and all 32 image files verified before staging.
Each worker passed verification before and after sampling. Both quiet
gates passed, workers joined, and raw traces were archived and checked
under the lease before RAM fixtures were removed.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Image verification and procedures](procedures.tar.xz).
