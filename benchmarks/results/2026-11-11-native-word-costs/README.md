# Current native text costs — 2026-11-11

One source-selection profile on fully qualified `fe6648b2`, frozen core `bf93c7ce`,
after R237 changed the inner loop. The same complete captured-helper/counter text
script runs on the existing quiet F4as_v7 VM with normal GC/tiering, diagnostic
perf maps and a same-process listing of `TryExecuteDictionaryLoop`.

5.873 execution seconds / 5.8 guest CPU seconds produce 1,151 leaves, including
58 unknown (5.04%). Native loop self accounts for 141 samples (12.25%), shared
dispatch 131 (11.38%), private cased-word preparation 91 (7.91%), MoveNext 46
(4.00%) and exact integer addition 42 (3.65%). Useful/inlined work and ancestry
overlap. These are not removable percentages, allocation attribution or a timing
comparison. No prior-source offsets are transferred.

[Raw maps/stacks/code/worker proof](observations.tgz), [leaf coverage](guest-cpu-leaves.json)
and [source gate](source-gate.json) preserve the uncertainty. All hashes, outputs,
quiet gates and joined cleanup pass; the archive was verified under its lease and
after retrieval. B578 is terminal/cached, with no users or RAM fixtures remaining.

Removing word materialization needs stronger identity/binding proof. Replacing
the admission scan with a constant worst-case word count can overcharge long
words or spaceruns by roughly 64×; it is not a reasonable streaming replacement.
Neither idea has a prototype or timing rejection. Preserve that source finding
without adding range arrays, wrappers or a new deferred-binding framework.

R239 instead corrects a demonstrated duplicate refund. R240 investigates a whole
private integer-total segment using the existing native loop, with ordinary
BigInteger publication at every exit. It requires semantic, ownership and
controlled source-pair evidence before retention; no predicted speedup.
