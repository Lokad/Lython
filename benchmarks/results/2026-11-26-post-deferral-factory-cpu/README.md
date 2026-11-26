# B1769 Callable construction after deferred definitions

The two warm synchronous profiles use the df71fc295 runtime and the
independently verified B1735c factory and escaping-rule workloads.
The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 26,
2026 UTC. Each worker warms for at least eight seconds, then samples CPU
at 199 Hz for about six seconds. Samples include execution, output
verification, request framing and result cleanup. These scripts perform
no file I/O.

| Observation | Ordinary definitions | Escaping rules |
| --- | ---: | ---: |
| Verified invocations during capture | 26,624 | 7,168 |
| All-thread samples | 1,160 | 1,275 |
| Samples with resolved runtime ancestry | 1,142 | 1,252 |
| Those with unresolved leaves | 60 | 90 |
| Nearest computed-argument call | 221 | 0 |
| Nearest closed pair member construction | 0 | 241 |
| Nearest direct operand return | 103 | 87 |
| Nearest boxed signed-integer test | 60 | 34 |
| Nearest admission reservation | 120 | 74 |
| Of those, nearest caller allocates defaults | 117 | 69 |

The boxed signed-integer test is common to both workloads. A separate
instrumented build counts successful machine-sized reads from immutable
compiler constants. Each row has the same counts for both hosting APIs.

| Complete workload | All boxed signed-integer probes | Machine constants in executable reads | Machine constants in closed sequences | Completed computed-argument calls |
| --- | ---: | ---: | ---: | ---: |
| Ordinary definitions | 7,728 | 5,120 | 0 | 2,560 |
| Escaping rules | 21,163 | 0 | 14,336 | 0 |
| Unicode text report | 9,226 | 6 | 6,144 | 0 |

Closed sequences also serve the text report's cased preparation. Include
that report as a third affected script when measuring prepared constants.
The file-fed CSV map has zero successful machine-constant reads. Use it
as the control. [Control route](control-routes.json). The counts establish repeated
classification of immutable values. They provide no speedup estimate.
The remaining default admission funds actual or potential published
storage and stays outside this prototype.

The next trial prepares signed constants once and retains the existing
literal objects for observable identity and Python fallback. It must
eliminate those runtime conversions in both evaluators. Compare warm
execution and compile-to-result with retained compiler admission and
preserve wide operands, live rebinding, cancellation and compiled-owner
cleanup. Prefer the current constant pool if extra metadata and
compilation outweigh the repeated work removed.

All 32 image files and both independent CPython goldens verify before
collection. Both quiet gates and post-capture goldens pass. Workers and
profilers join, guest fixtures are removed and the persistent archive
verifies under the lease. The instrumented run also verifies all output,
files and allowance return for both hosting modes on all three scripts.

[Attribution](attribution.json) · [Route counts](routes.json) ·
[Identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz).
