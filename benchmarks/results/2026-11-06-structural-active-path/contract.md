# R02 structural active-path consolidation, before implementation

Production bf741ec2, frozen df1f1f2c. B328 complete compound-key aggregation:
5.5guestCPU/1096samples (record is authoritative),69prefix uniquely mapped,
157unknown retained. PyEquality149leaf, tuple hash60, pair HashSet.Remove30,
single HashSet.Remove21. Dictionary work566inclusive overlaps its hash/equality
children. Locks109/TLS48 remain unassigned; they are not all cycle-table cost.
R01 method-CSV only75inclusive call-advancement/730samples, including useful work;
no new frame pool or absent packaging phase justified. Both captures terminal,
full goldens/quiet/Closed/funding/joins/fixtures pass; no gain prediction.

Hypothesis: active structural hash/equality paths pay repeated identity hashing,
two table authorities and reversed-pair probes beyond actual value work. Replace
the two HashSets with one vector of active identity keys in the existing per-run
StructuralGuardState. Pair entries hold left/right; single entries have a null
right. No general object inventory, heap tracing, lookup cache or new owner.

Deletion: StructuralPairComparer/identity-hash combination, Pairs and Singles
tables, opposite-pair HashSet lookups, old Scope's duplicate pair/single/kind/active
fields. Scope keeps its exact state and identity key; removal searches from the
end and removes the matching entry, preserving existing out-of-order disposal
without index invalidation. Depth-only scopes never allocate cycle storage.
All entries clear their references on unwind. Runtime callers remain unchanged.

Contract unchanged: detect identical/reversed active pair cycles immediately;
single and pair traversal of the same object remain independent; noncyclic alias
repetition after unwind stays valid. Keep managed depth and actual managed stack
checks exactly as implemented, explicit async execution state across thread hops,
current cancellation/first-refusal checks, ambient scopes and Python semantics.
No new policies/options/counters/watchdogs, weak references, forced GC or budget
infrastructure. Existing coarse execution metadata ownership remains.

Tradeoff: active membership/removal becomes O(depth), bounded by existing512-level
managed admission (or tighter configured Python depth), rather than expected O(1)
hash tables. Ordinary shallow paths lose identity hashing/table maintenance;
deep nesting can do more comparisons. Do not raise depth limits or relax tests.
This is not a promise of CPU/allocation/embedding improvement.

Focused structural/hash/equality/managed-depth/cancellation/refusal/async-isolation
and shared ownership Release checks, independent mixed hash/equality Python
controls, both actual runtimes. Exercise independent kinds, reversed cycles,
cross-thread and out-of-order disposal, reentry/reuse after failure. Existing deep
and original stress coverage remains. Then one existing four-job compound2/8,
text/CSV comparison with two identical current roles and production reference,
~120s/owned160s, normalGC/tiering/full goldens/quiet/cleanup/funding gates. Preserve
adverse results; bounded refinement allowed if evidence warrants. Decide deep-path
tradeoff and regression debt before retaining. Full platform/stress/installed
qualification batched at coherent delivery; publication postponed.
