# B01 source decision and bounded prototype contract

Source1134d6e8, current canonical PyDict and native return proofs retained.
Reject a separate shared-key record format for this prototype: native/bridge
assumptions, custom-hash reentrancy, iterators across representation promotion
and native-return eligibility require excessive parallel machinery.

Selected: one private immutable construction template in CsvDictReader, using
normal canonical PyDict storage. Values in this template are Python integer input
positions; each independent row clones stored hashes/keys/order and fills values.
No alternate dictionary backend, live shadow key index, parser view, pool or new
memory infrastructure. Remove repeated per-row generic hash/probe/insertion and
replace HasStringFieldNames with identity-checked reusable header preparation.
Field-name reference identities and count are checked each row: equal replacement
strings must not accidentally reuse old key identities. Duplicate names preserve
first key/insertion order and last field value. Existing rest-field handling stays.

Both the header snapshot and canonical template are admitted before allocation;
conservative cache/replacement funding stays with the existing batch/root owner,
including retained row key aliases. No per-object ledger/refcounts/weak tracking.
Rows own independent canonical storage, preserving copy/mutation/iteration/protocol
callbacks and NativeBody reads. EOF materialization/source disposal are unchanged.
No speculative callback/sink or PyList version/invalidation subsystem.

Capacity/overlap and irregular rows must preserve reasonable working storage.
If template reuse cannot avoid pathological rebuilding or larger row capacities,
refine the design explicitly before timing. Custom/non-string fieldnames keep
existing semantics and callbacks. Preflight refusal remains terminal, no retry or
fallback after actual work; cleanup stays awaited and original denial retained.

Focused CSV/record/native-return/cancellation/ownership regressions and independent
Python controls before a bounded existing complete-job comparison: ordinary and
variable-width CSV, plus one text/JSON control. ~120s expected/160s owned, normal
GC/tiering, unchanged control/goldens/quiet/lease/join/funding/fixture gates. Allocation
separate only if material. Retain every adverse result, no automatic promotion or
fixed percentage veto. Complete full correctness/capacity/ownership before merge;
platform/stress/package qualification batch later. No broad campaign or source edits
in the sibling regex repository. Publication remains postponed.

Pre-measurement capacity refinement: keep the template genuinely immutable, with
its original header capacity. Each row admits the same normal/extra capacity as
before. Equal-capacity rows clone the canonical table; larger rest-field rows
append keys/values using stored hashes into the admitted table. No cache growth,
rebuild on alternating widths, permanently doubled normal-row backing, or second
dictionary format. Initial source patch and passing checks remain private history.
