# D64E contextual nested hashing — source contract before code

Baseline f2b7b8a7/frozen8050cc0f. B1/B2 focused qualification and original
synthetic+5%/final+1.8-2.3% debt remain; earlier aec4f785 alone fully qualified.
No assumption that generated/custom-key costs explain ordinary native scripts.

E1: hash slices through the existing contextual hash entry, including direct
__hash__, tuple/cache/container use and start/stop/step order. Preserve native
hash combination and explicit error origins. Extend existing managed structural
guards to recursive slice hashing/classification; never introduce native probes.
Keep cancellation/refusal checks at actual guest descriptor/hash boundaries,
without adding fuel or checking every scalar in a native tuple walk.

E2: generated dataclass __hash__ retains its defining field plan. Read all included
fields in order before hashing any, matching Python's generated tuple expression.
Use one funded temporary values array and the existing contextual tuple hash;
no tuple wrapper, second evaluator or memory layer. Reserve before allocation or
field callbacks, check sticky failure after callbacks, and hold storage through
all users. Direct/base hash calls must not substitute the runtime subtype plan.
Remove the public shortcut through PyInstance.GetPyHashCode/type hash metadata;
keep structural callers until their real dependencies are migrated.

E3 REQUIRED, not silently declared solved by E1/E2: current structural dataclass
hashing reads fields using ambient guest access. Equality's suppression does not
prevent ReadStructuralField from using ReadField. Making storage identity-based
can retire the structural field-hash loop, but raw semantic HashSets still serve
Counter algebra/comparison, mapping rest patterns and view comparisons. Changing
global instance hashing/equality without migrating those callers would regress
supported semantics. No automatic broad comparer rewrite or duplicate backend.
Record this coupling in PLAN; separately prove the removal/replacement invariant
for those finite callers before the identity migration. Preserve working descriptor
keys meanwhile; do not reject behavior merely to make the old loop unreachable.

Acceptance: independent CPython public goldens for native/custom nested hashes,
field-read-before-hash order, inherited/direct generated methods, failures and
repair; existing hash/tuple/dataclass/stack/copy/reentry suites. Actual callback
cancellation/refusal cannot continue later guest callbacks or mutate targets;
owners unwind and return allowance without GC. Key hash callbacks still require
synchronous completion; no new async framework. Focused Release actual11/10 during
iteration; one short affected native-workload pair at coherent retention, with
valid baseline semantics. Batch full platform/Stress/installed at combined delivery.
E1/E2 do not complete the whole-script performance objective or E3 contract.

Preparation selects included defining field names once under the already admitted class plan; each invocation owns one values array. Tuple/slice classification guards were added conservatively; the nested native grouping case specifically tests their cost. Existing structural field hash loop remains a live E3 dependency, not hypothetical scaffolding.
