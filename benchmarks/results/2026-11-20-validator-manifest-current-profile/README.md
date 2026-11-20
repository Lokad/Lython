# B1245 generated validator and guarded manifest CPU attribution

This quiet-VM profile uses `bd94edeec` on November 20, 2026 UTC.
The Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3. Both CPython
goldens and all 32 worker image files verify before collection. The
profile keeps normal tiering and enables the managed perf map only.
Samples cover complete warm worker batches with mediated RAM-backed
file I/O, goldens and cleanup. Compilation and startup sit outside
the capture.

| Samples or nearest resolved runtime method | Validator | Guarded manifest |
| --- | ---: | ---: |
| All process samples | 1,337 | 1,387 |
| Samples with resolved runtime ancestry | 802 | 910 |
| Fixture ancestry outside resolved runtime | 518 | 466 |
| Unmapped leaf samples | 768 | 837 |
| Opened byte file acquisition | 158 | 328 |
| Executable instruction dispatch | 85 | 28 |
| Memory reservation | 41 | 32 |
| JSON decoder loop | 37 | 5 |
| Closed receiver selection | 30 | 2 |
| Subscript assignment | 27 | 0 |
| JSON item insertion | 22 | 0 |
| Native dictionary storage copy | 16 | 1 |

The validator reads 96 fields, including six malformed integers and six
negative numbers. Many
other fields take the normal conversion path, and the helper always
builds a result dictionary. The guard manifest reads configuration and
dependency files, then writes two JSON files. The acquisition samples
include inlined host work. The earlier [completed-acquisition
experiment](../2026-11-19-opened-file-acquisition/README.md) did not
establish a whole-script gain. The profiles do not isolate enough call
transport to justify a frame rewrite. Dictionary construction and
query costs motivate a comparison that removes generic key protocol
dispatch for exact string reads and private record writes.

Both workers verify before and after sampling and join on exit. Both
quiet gates pass. The persistent trace archive was verified under the
VM lease before guest fixtures were removed.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Configuration and procedures](procedures.tar.xz).
