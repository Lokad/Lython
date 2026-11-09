# Current short configuration-editing phases

R159/B509,2026-11-09. Complete maintained `config-edit.py`: read181B JSON, edit
fields, append a path, overwrite the file and print the resulting path count.
The same source, input bytes, full stdout and output-file digests run on both engines.

| Phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Compile and dispose |36.5–36.7µs|34.4µs|1.06–1.07×|
| Warm execution |78.7–79.0µs|61.6–62.1µs|1.27–1.28×|
| Compile-to-result |138.7–139.6µs|124.9–125.1µs|1.11–1.12×|

Ratio above one means slower. Two independent processes per engine, seven
rotated150ms rounds after5s warmup each. Ranges span process medians, and ratios
include all four pairings; they are not confidence intervals. Lython/Python control
spread is0.68/0.003% compile,0.36/0.76% warm,0.67/0.18% complete. Maximum IQR2.98%.
The existing Diagnostic status describes observed stability, not an acceptance veto.

Measured production is **7c10322c-equivalent**, current documentation-only038f7fb4,
core **cfeb99b0**. Reuse the verified nonincremental freeze built before commit from
3fb plus the committed R157 source; every production source byte matches. This is
not a new post-commit binary or an individual-optimization speedup comparison.
Common frozen workerfec79/optional adapter0.4, actual.NET11RC3 and stock
CPython3.13.16 on the established F4as_v7/Ubuntu24.04.4/4AMD EPYC9V45vCPU VM,
16GiB/no swap, ordinary GC/tiering. Exact hashes and Ready identities are retained.

Warm includes mediated RAM-file reads/writes, complete output validation and
prior-result cleanup. Compilation includes construction and prior-script disposal;
complete combines compilation and execution. Startup, fixture reset/protocol and
final cache teardown are excluded. CPython compiles source directly and keeps
imported modules; Lython isolates execution imports and applies host mediation.
Independent phase medians need not add up. External process CPU also includes
fixture reset/protocol, so it cannot identify an internal CPU cost or scheduling gap.

Three jobs finish88.416s under a150s owner/30s stop/30s margin. Every source/auxiliary/
reference/golden/ownership/quiet/Closed/join/RAM/archive/localSHA gate passes; no
users or fixtures remain. Payload links existing persistent images, with no new
core copy. Original240MiB OS floor and12MiB headroom remain. Preparation unnecessarily
removed one redundant archive despite adequate fresh free space; its exact tracked
bytes, mode, owner and group were restored before collection, without overwrite.
Both original action and restoration proofs survive; raw evidence was untouched.

This closes the current config timing gap. It does not establish an initialization,
JSON, path/I/O or handoff cause, explain older sparse captures, or prove another
source's performance. Only the config rows replace historical4598 measurements in
the current guide; all old rows and failed/adverse conditions remain archived.
The next bounded investigation attributes current config execution before selecting
one coherent redesign; no automatic cache, type sharing or stack-policy change.

[Summary](summary.json), [source proof](core-source.json), [identities](identity.json),
[raw workers/results](observations.tgz), [original guide/preparation](preparation.tar.xz),
[restoration proof](unneeded-cleanup-restored.json), [terminal status](terminal-status.json).
