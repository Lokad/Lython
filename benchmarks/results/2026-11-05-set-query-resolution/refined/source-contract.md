# D64B actual-hash authority — before code

Retained aec4f785 has full4154internal/16015public per actual11/10 passing;
CI42100526619 active. The stable dataclass structural-collision probe demonstrates
three extra equality callbacks for unequal actual guest hashes (Python zero).
This is an observable correctness defect, not a mutable-hash conjecture.

Use actual hashes exclusively for guest equality candidate selection. The existing
structural index remains for set stored-entry identity/hash reuse: it is not dead.
Keep one existing index, preserving insertion-ordered positions. Register a position
only once if actual/structural hashes coincide, and update rollback/removal together.
Within one synchronous snapshot no guest code/await mutates buckets; count/filter
one actual bucket, allocate one final array and fill in that existing order. Delete
candidate HashSet, sort, intermediate lists, CollectCandidates, builtinOnly mode,
structural selection argument and missing-index alternate scan. Side lists and
indexes are created/cloned together; an aborted unpublished object is not reusable.
Keep existing conservative candidate reservations initially; no hidden allowance,
prehash wrapper, per-object ledger or new storage/dispatch framework.

Raw-storage fallback must not compare already-indexed protocol keys again, or
accept native structural hashes in place of the query's actual hash. Existing
native insertion filtering already classifies protocol keys; apply that invariant
consistently to lookup/removal too. Class mutation and arbitrary mutable native
hashes have additional limits; do not claim universal key-mutation correctness.
Preserve native/custom same-actual-hash matches, identity, callback errors/reentry,
insertion order, first refusal/cancellation, retained snapshots and cleanup.

Affects custom/compound keys in complete CSV/text/JSON aggregation and custom-key
scripts. No claim they dominate native representative workloads. New public
CPython goldens for unequal custom hashes and changed-hash query behavior;
existing collision/order/copy/reentry/async-comparison/refusal/ownership checks.
Focused actual11/10 checks first. One short affected comparison only at coherent
retention if a performance claim is useful, not a new framework/campaign. Do not
claim speed from removing callbacks that Python requires. Missing empty/native-
only set query hashing and nested slice/dataclass callback dispatch are separate
source findings requiring explicit follow-up, not silently declared solved.

# D64B2 complete custom-set query resolution — before code

Committed f64bc7c5 actual-hash candidate selection passes1397public/461internal
per actual11/10; earlier aec4f785 delivery CI42100526619 still runs independently.
Isolated CPython/public probes demonstrate empty custom membership omits hashing
and native-only membership misses an equal custom key. Current Add/Contains/Remove
also duplicate side-candidate scans; Contains lacks native fallback and returns a
stale match after equality clears the set. This is one query pipeline problem.

Share one synchronous custom-set resolver using existing actual-hash snapshots,
insertion sequences and native fallback. Each public operation hashes once before
empty misses. Add/Contains/Remove consume one resolved retained item; pop/internal
cleanup keep no-guest stored removal. Recheck sticky refusal/cancellation after
hash/equality callbacks and verify the candidate is still retained before use.
Sequence changes restart candidate selection without repeating earlier side
entries. Keep explicit native-only fallback, capacity preflight and snapshot owners
through actual callbacks. No new engine, async framework, per-object ledger, policy,
cache, enum dispatch or configuration; synchronous key callbacks remain explicit.

Delete the three independent contextual lookup/collision/fallback loops rather
than wrapping them. Keep context-free structural helpers for real internal uses.
Set-to-set genuine async equality stays on its existing awaited path. General key
slot mutation/mutable native hashes and nested slice/dataclass hashing are separate
limits; do not reopen tracing or arbitrary reference reclamation.

Acceptance: independent CPython goldens for empty/native matches and query failures,
custom hash callback counts, equality clearing the set during contains/add/remove,
existing reentry/copy/iteration/async-comparison/ownership/cancellation/refusal
checks on actual11/10. Compare only at coherent retention, with correct baseline
controls; do not attribute speed to removing required callbacks. Full stress/
platform/installed qualification batches after this coherent set migration.

Correction: positive equality after set clear is accepted by Python add; membership/removal require a current entry. Preserve tested set-to-set stored-hash reuse using canonical current side entries, without structurally rehashing mutated keys. Entry metadata is now reserved before index/backing/member publication, with existing rollback and retained-capacity funding.
