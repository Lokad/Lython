# RAM-backed text normalization attribution

B980 profiled the 4,096-line, 52,224-byte normalization fixture on the
quiet Azure F4as_v7 VM. It used the exact `b810f660b` production source,
core `93739eed`, and the verified 32-file worker. The complete command and
its output file matched the existing goldens. Guest fixtures were RAM-backed.

The capture contains 992 all-thread CPU samples, with 215 unresolved leaf
symbols. Of those samples, 890 contain a resolved runtime ancestor and
100 show fixture work outside that ancestry. The active execution thread
used 4.49 seconds of CPU across 4.66 seconds of recorded guest elapsed.

| Inclusive group inside resolved runtime ancestry | Samples |
| --- | ---: |
| Join or its leased drain | 447 |
| Generator execution | 416 |
| Split construction | 106 |
| Right trim | 106 |
| Host I/O | 89 |
| Replacement | 50 |

Groups overlap. Inlining and incomplete stacks affect the counts. This
capture attributes a complete warmed workload and makes no removable-CPU
or speedup claim. The measured script first splits the source, then calls
`rstrip` in a generator, and joins the fully drained output. The generator
and drain give a larger candidate than another isolated trim kernel.

Prototype a private exact split/trim/join path. Reuse the compiled call
boundary and existing UTF-8 delimiter and whitespace scans. Prove the
outer consumer, actual builtin receivers and generator body before skipping
the intermediate list and generator. Keep arbitrary iterator effects,
exception precedence, current cancellation and governed final allocation.
Verification must compare whole scripts and the existing generic fallback.

Both quiet endpoints passed. Workers joined and all RAM files were archived
and hash-verified before unmount. Staging retired only the exact, Git-archived
older `7170869d` core replicas.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Source and verification](checks-and-procedures.tar.xz)
