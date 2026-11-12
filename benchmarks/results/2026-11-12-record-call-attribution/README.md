# R267 — remaining CSV/report work

2026-11-12, retained `da9e5066`, frozen a55202fb, same fec79 worker and optional
adapter0.4. Two current-image Run captures on quiet F4as_v7 / Ubuntu24.04.4 /
.NET11RC3, ordinary GC/tiering plus recorded perf-map/JIT diagnostics. Total guest
execution clocks 11.771 seconds; selected CPU 5.98 seconds CSV and 5.38 report.
All actual-image, stdout/files, allowance, verification/close, quiet, join,
unmount and independently verified archival gates pass. No elapsed comparison.

| Complete script | Selected samples / unknown | Instruction/input/result/comparison | Binding/entry/work | Other visible work |
| --- | --- | --- | --- | --- |
| CSV validation | 1,189 / 70 (5.89%) | 267 (22.46%) | 90 (7.57%) | CSV scanner/materialization/adoption 129; shared-root lock entry/exit 41 |
| Report | 1,068 / 68 (6.37%) | 196 (18.35%) | 17 (1.59%) | Formatting/write 160; shared-root lock entry/exit 72; JSON construction/string work also present |

Groups cover saved top-24 leaves only, not complete exclusive subsystem costs.
Useful/inlined work is included; these shares are not removable taxes or speedup
forecasts. Whole-batch sampling includes mediated RAM I/O, reset/protocol/output
validation and cleanup beyond execution clocks. Raw stacks/maps, thread CPU,
unknowns and all diagnostics survive in observations.tgz.

Source gate: there is no duplicate frame or second argument array. Ordinary
deferred helpers already run without an invocation context until actual protocol
or namespace requirements; binding locals and operands share backing. CSV already
admits all normal field payloads together, stores small strings inline, and adopts
row storage into dictionaries with shared keys. Report fields already append
direct UTF-8 into transferred final storage; constant specifiers are prepared.
Do not reintroduce those completed projects as new optimizations.

The remaining broad lead is instruction/operand transport, not a binding-loop or
per-character-string patch. A bounded source proof must identify an actual stage
to eliminate before another prototype. Prior arenas added access/save/restore
work and measured slower; integer-local variants saved allocation without a
general CPU gain. These observations allow a materially different refinement,
but do not support an unchanged replay or a universal tagged-value commitment.

Important obstacle for whole-body preparation: report field formatting may admit
storage before a later dictionary read. Host reentry can mutate that dictionary.
Reading all values early, or restarting ordinary execution after effects, changes
semantics. Any consolidation must preserve ordered reads, live intermediate
values, original spans, actual suspension and funded cleanup. No second mini-VM,
extra dispatcher or hidden memory grant is selected from these leaf counts.

This closes the two-capture investigation; do not repeat it unchanged. Next is a
concrete operand/lifetime deletion proof, or explicit obstacle followed by another
stage. The performance objective remains active. Exact-head full delivery is
tracked separately; no new CPython ratio, allocation or final adapter claim.

The retrieval time guard initially rejected a local attempt 23 seconds before
the existing observation bound; it contacted no remote process and changed no
conditions. Retrieval after the bound succeeded. Only exact committed B614
archive/catalog/record duplicates were retired, with fresh lease, UID/path/inode/
all-alias and canonical Git/SHA proofs. Current/reference images remain protected;
OS/evidence floors unchanged. Terminal state is cached; no live users/RAM traces.
Publication remains postponed.
