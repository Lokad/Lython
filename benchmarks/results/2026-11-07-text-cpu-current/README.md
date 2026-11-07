# Current complete-text CPU attribution — R63/B419

2026-11-07, retained source `70267889`, exact core `74c88248`; normal optional
0.4 adapter and unchanged checked worker. R62 full delivery is independently in
flight. This is an attribution, not a before/after comparison or speedup claim.
Older text captures predate operand/call changes, justifying this bounded refresh.
No production changes or additional execution route are introduced.

One maintained `scripts.text-summary-helpers.large`: helper returns
`line.lower().split()`, a dictionary counts words, a scalar counts totals, then
sorted output is written. Dedicated F4as_v7/Ubuntu 24.04.4/.NET 11 RC3. Six guest
seconds of warmup, two-second settle, six-second wall capture at 199 Hz using
cpu-clock/FP. PerfMap3 and five declared JIT listings are diagnostic overrides;
do not compare these elapsed times with normal-settings CPython measurements.
The capture includes complete verified jobs and protocol gaps; attribution selects
the actual named guest worker by CPU deltas, retaining other threads separately.

**1,086 guest samples / 5.47 guest CPU seconds**, 65 unknown leaves (5.99%),
no malformed map lines, empty or single-frame guest chains. Accounted complete-job
elapsed is 5.546 seconds. Pre/post full stdout/file goldens, exact core/auxiliary/
Ready, quiet endpoints, Closed/exit zero/join and empty fixtures pass. Persistent
records remain separate from the owned 128 MiB tmpfs guest fixtures. Archive
verified under lease and locally; all users joined and fixture mount removed.
120-second owner + 30-second stop + 30-second margin; observation after 180 seconds.
Terminal and cached: never query B419 again. Original raw stacks/maps/perf data,
JIT listings, catalog, all thread CPU deltas, unknowns and sources remain archived.

| Leaf region | Samples | Interpretation |
| --- | ---: | --- |
| Instruction prefix | 184 | Dispatch plus inlined instruction/control work |
| Input instruction execution | 73 | Operand reads, native/protocol selection and operation work |
| Exact integer addition | 60 | BigInteger arithmetic, admission proof and result box; not boxing alone |
| Whitespace collection | 52 | Useful token-boundary/case inspection |
| Stack transfer | 39 | Existing operand/value lifetime handling |
| Structural hash dispatch | 38 | Dictionary key work; actual string hashing is separately inlined/called |
| Native call dispatch | 36 | Builtin/helper selection, including useful inlined work |
| Dictionary get | 33 | Lookup and key/value handling |
| Native helper expression evaluation | 31 | Already bypasses ordinary binding/frame creation |
| Subscript store | 30 | Mutation/protocol and storage handling |
| Complete local result | 27 | Direct-result publication and cleanup/continuation handling |

Inclusive native-call 447 / frame-free helper 308 / split 208 overlap: do not add
them or call them removable packaging. No explicit binding/frame-construction leaf
dominates; inlining means absence of a leaf does not establish zero cost.
Native lower/split, borrowed operands/returns, shared dictionary storage and
deferred contexts already exist. Ordinary string dictionary keys already use
synchronous native lookup. Integer counters already avoid magnitude reservations;
instruction metadata is compact, not a previously unnoticed large object.

## Source/development decision

The remaining instruction/value publication boundary is a justified design target.
This does not prove that deleting a tiny input carrier will help: the reader can
inline and scalar replacement may erase it. A mere parameter/attribute rewrite
or another call arena is not selected. Assess the complete operand/result and
cleanup-continuation boundary: producers currently retain an adjacent store,
decode its target/span at publication and preserve it for delayed disposal.
Any redesign must remove a complete repeated dispatch/storage phase, preserve
the existing native-reference lifetime boundary, and close with deletion of the
displaced publication route. Do not erase cleanup checks or observable store spans
to manufacture a fast path. Source facts and generated code must select the
concrete design before implementation; no predicted percentage or causal saved-CPU
claim follows from the samples. If it requires a parallel dispatcher/new ownership
framework, record that cost and select another boundary rather than accumulating it.

The capture investigation is complete; the whole-script objective remains open.
Later redesign needs defaults/closures/protocols/evaluation order, cancellation,
first refusal, retained aliases/pending borrows, sync/real async and awaited cleanup
acceptance, plus one bounded whole-script source comparison and unchanged controls.
Core work remains independent of producer publication; publication is postponed.

Persistent headroom was maintained by deleting only two redundant extracted B415
core copies after verifying remote/local immutable archive hashes and exact archived
member bytes. Both immutable archives and actual worker images remain. The exact
paths/hashes and guarded command are retained; no sole evidence was removed.

[Summary](summary.json), [all leaf/inclusive chains](guest-call-chains.json),
[declared contract](contract.md), [archive receipt](archive-receipt.json),
[raw/source/JIT/catalog evidence](observations.tgz),
[local launch/retrieval/headroom records](local-collection.tgz).

Follow-up [R64 source assessment](publication-source-gate.md) finds the common
path already skips that store's dispatch and result write. A complete removal
would add another metadata/cleanup-continuation representation, so no prototype
is selected. Existing prepared binding/class-cell facts are the next frontend
consolidation assessment; the broader runtime performance objective stays open.
