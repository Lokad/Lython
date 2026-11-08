# Current helper execution source gate

R123/B482, 2026-11-08, current6f76/corec617. Existing normal B480 complete CSV
validation/helper versus independently CPython-verified inline control is roughly
5.1 versus3.3ms. The gap includes real calls, extra bindings, returned tuple creation,
unpacking and useful dispatch; it is not a measurement of pure call overhead.

Compile-only exact-core census finds validated_record has13instructions,3lookup
slots,3locals,no exceptions,mirroring or native body. Its exact native CSV data can
run wholly in the canonical borrowed prefix: int, immutable-layout field reads,
numeric/string comparisons and final tuple construction. No per-call context object
or local-array allocation is needed. Tuple construction transfers its private final
array; exact tuple unpack already borrows that immutable array in both APIs. CSV
record construction already adopts parser arrays. Those hypothesized copy phases
do not exist and cannot justify another collector, arena or tuple transport route.

Two existing six-second CPU captures, not normal elapsed evidence: helper1208leaf
samples/6.08guestCPU/57unknown4.72%, inline1174/5.90CPU/69unknown5.88%. Actual function
context construction has0leaf in both. Helper binding/workspace60leaf4.97%, frame
preparation50leaf4.14%, tuple construction12leaf0.99%; groups overlap. Prefix169leaf
13.99% versus13511.50% mixes checks/control/useful work. Inclusive frame501samples
includes nested prefix work, not a removable42% tax. No inference of zero cost from
inlined0leaf categories. New type-proof or universal value/stack rewrite is unjustified.

Source shows generic named-argument binding still reconstructs CallArgumentValue
views and resolves static keyword names against the immutable parameter layout on
every call. Positional-only binding is a separate loop. Both own the same admitted
workspace and return a BoundCallArguments descriptor whose presence remains useful
for protocol defaults and builtin consumers. Deleting presence globally or deriving
the storage owner from a guessed closure would break real contracts. A new narrower
carrier would repeat the slower earlier call-control direction.

**No runtime prototype selected from these samples.** A coherent next candidate is
consolidation of ordinary fixed-shape argument-to-local binding: prepare static
keyword-to-parameter positions when immutable layout identity permits, reuse the
existing lookup lifetime, keep current defaults live, and share ordinary positional/
keyword local initialization. Remove repeated wrappers/name resolution and displaced
duplicate binding logic; preserve the general protocol/expanded call binder.
This remains a source-gated candidate, not an assumed gain or automatic promotion.
If it needs a new cache owner, dispatch framework, ledger or duplicate binder,
reject that form and record why. Bounded before/after must include complete primary
validatedCSV/report, ordinary native CSV/text controls, compilation/allocation and
two unchanged process controls; actual suspension/refusal/recursion/default mutation/
evaluation order/retained aliases/budget reuse are semantic gates.

This closes the attribution question, not the performance objective. No further
capture without new source-specific uncertainty. Keep regex independent. All original
failed collection/archive conditions survive; no remeasurement or percentage veto.


Inventory correction: the original compile-only census.json used the initialC54
prototype (80/128Bframes), while the actual timed profiles verifiedC617. The frozen
reference preflight for R124 caught this path mismatch before any comparison ran.
current-census.json is regenerated atC617(72/120B); all instructions/operations/
counts/cache/local/native properties match. Original inventory remains intact.
[Correction](census-correction.json). No changed timings or remeasurement.
