# R119 flat lookup entries and shared record fields

Hypothesis: complete report/CSV/JSON scripts repeatedly resolve constant string
fields on rows which already share immutable key layouts. The normal executable
subscript path still hashes/probes each row. Existing generic member caches also
allocate individual record objects. Replace these with one flat funded entry
array, reusing it for field positions. Do not cache rows/values, mutate layouts,
add an opcode/dispatch route or change public options. General dictionaries,
custom protocols, missing/deleted fields and changing keys keep canonical reads.
R108 dictionary work ~13% leaf and R116 hash/get/store samples motivate the source
hypothesis; neither proves a dominant removable share or predicts a speedup.

Funding: retain existing conservative per-slot admission before allocation.
Cache backing allocation can call the host and mutate rows: acquire backing first,
then prove current immutable layout/key/live slot. Cache stores only layout,
borrowed immutable key and integer slot, never a row value. Drop all entries at
frame cleanup before refunding capacity. No admission between final proof/read.
Cancellation remains at instruction and native lookup/read boundaries. Missing
slots use canonical KeyError and custom keys/mappings preserve actual suspension.

Completion: tests for repeated reads across independent rows, mutation/deletion/
reinsertion/layout changes, dynamic keys, missing fields, generic/custom cases,
admission reentrancy/refusal and cleanup/retained aliases; focused actual11/10.
Compare unchanged complete five-case catalog normalGC/tiering, full goldens and
zero balances, compile separately. If focused diagnostics warrant retention,
one bounded quiet source pair with unaffected text/float/custom controls, preserve
all adverse data; full platform/Stress/installed-core at retained delivery. Do not
claim CPU from allocation. No fixed percentage veto; decide debt explicitly.


Pre-measurement source refinement: render(record) in the maintained report uses
native-return proof and bypasses ordinary executable field caches. This prototype
therefore does NOT eliminate those native-helper lookups. CSV module field reads
are the directly affected complete workload; unchanged report is a reach/control
case. Do not attribute its dictionary leaf share to this new cache. Allocation
may increase for cold sites because flat entries are larger than old reference
slots. Preserve that cost and compare compilation independently.

Validation correction: the broader initial filter accidentally includes original Stress tests. Preserve its outcomes, never relax bounds; subsequent development filters explicitly combine Category!=Stress. Initial targeted19internal/14public pass is separate. No claim that this is a minimal iteration filter.


Initial result and correction (before refinement): all semantic/ownership gates
pass; compile allocation unchanged; CSV desktop timing promising, text adverse and
controls mixed. Report allocation +9.02% is reproducible. Metadata census corrects
the earlier source inference: render is NOT native (13instructions,3new lookup
slots); multiple mutable formatted fields use canonical ordinary execution. Each
of3072 calls therefore allocates a new96B cache array. 3072*96=294912B agrees with
observed~295KB excess. Preserve initial source/core138b6a09 and all adverse data.
Do not describe report as a native-return cache bypass in initial limitations;
those original statements are retained as corrected evidence.

Bounded refinement selected: allocate record-field cache slots only within an
existing explicit loop or eager comprehension in that invocation. A once-executed
instruction cannot reuse its frame-local lookup; avoid creating that storage.
Use existing compiler loop/comprehension facts, no new graph pass/policy/framework.
Generator-expression field caching is an unimplemented opportunity, not a syntax
restriction. Member caches retain existing semantics. This does not solve lookup
reuse across successive calls: keep that larger opportunity separate. The record
admission test now places its read in a loop to exercise actual reusable storage.
Recheck focused semantics both targets, then one same-driver diagnostic. Quiet VM
comparison is justified only for an explained candidate with preserved controls.
