# D64A protocol query routing correction — before code

Baseline2c9f0988, productionafc3189e/frozen084aaa38. Full4150internal/16004public
per actual11/10 pass; independent CI42088051016 active. D63 source/profile preserved.
No assumption that old hash/call samples represent removable work.

Isolated local CPython3.13.2 vs actual public Lython probes demonstrate stable-key
read/get/contains/delete call __hash__ only once total (insertion) instead of five
calls, and changed callback ValueError is suppressed on query. Existing native
identity probe occurs before NeedsProtocolKey in dict/set lookup/removal. Reorder
using existing protocol helpers; delete redundant late protocol branches. Native
builtin keys and actual stored-key/internal cleanup/copy APIs stay distinct.

Scope: dictionary TryGetValue both contexts, Remove/TryRemove, set Contains/Remove
and any same public identity shortcut. Use existing protocol hashing/equality/
snapshot ownership. No prehash wrapper/ledger/cache/policy/dispatcher/new fields.
Calls now required by Python can allocate/refuse/suspend or raise; those errors
and first refusal must propagate, target storage unchanged until admitted mutation.
Do not suppress errors or bypass callbacks for an apparent speedup. Preserve
stored-key merge/copy/no-guest-cleanup behavior and insertion/iteration semantics.

This is the FIRST correction, not a claim that every protocol issue is closed.
Actual-hash vs structural-index authority and nested error-value origin are
separate explicit followups. StructuralHash has a real stored-identity consumer
in PySet, so not dead. Delayed __hash__ remains unqualified/synchronous helper;
no new async framework is authorized by this bounded change.

Acceptance: independent public Python goldens for dict/set stable callbacks and
raised errors, protocol tuple keys, identity and side effects, sync/async APIs
where callbacks complete synchronously. Existing protocol/reentry/copy/refusal/
cancellation/ownership regressions. Small gated refusal/cancellation cases only
for a real missing path; await cleanup and verify shared allowance/root baseline.
One short affected complete native-workload source/control pair only at coherent
retention; no protocol timing against an invalid callback-skipping baseline.
Full platform/package/stress at retained delivery, no duplicate prior qualification.

# D64C/D hash error origin and linear key validation — before code

D64A protocol query routing/copy correction has68public and42internal focused
cases per actual11/10 passing; original source/copy mismatches and one failed
new-copy assertion preserved. Existing AsyncBoundaryContract explicitly rejects
suspending dict/set/cache key callbacks; preserve that supported limitation.

Demonstrated CPython mismatch: hash(slice([],1)), emptydict.get, dict literal,
set membership all name outer slice in Lython instead of failing list. Existing
PyUnhashableException carries no value; catchers reconstruct the outer root.
Carry the actual value in this EXISTING typed internal failure (error-only field),
normalize once via an int-returning structural-hash helper. Builtin boxed Python
hash remains a wrapper; no BigInteger allocation during mere validation. Do not
catch arbitrary InvalidOperationException as hashability. Remove obsolete
EnsureHashableValue and BucketHash duplicate funnels where real callers share
normalization; preserve typed failures where opaque CLR set membership needs them.
No successful-path storage fields, cache/ledger/policy, or async framework.

Shape/cycle validation must remain before recursive protocol classification.
Make the existing shape walk pure: validate tuples recursively WITHOUT hashing
every child subtree, then one root structural hash validates actual hashability.
Sets and standalone views still require that root preflight before opaque storage.
Native dictionary storage still performs its actual hash: do not pretend this
bounded migration eliminates that separate duplicate or drop guards for empty
lookup/namespace/generated dataclass. No broad native backend redesign.

Eliminated work: quadratic repeated subtree hashing for nested tuple keys; one
error-normalization mechanism instead of duplicated broad catches/wrappers.
Preserve tuple/element identity, disabled hash slots, actual protocol hash counts,
cycles/managed depth, effective cancellation during existing structural walks,
capacity/replacement admission before mutation and retained/snapshot funding.
Native copy uses existing stored data/hash metadata; never re-query guest hash.

Acceptance: independent CPython goldens for nested slice/tuple/namedtuple/namespace
errors and cache keys, existing hash/query/tuple/protocol/reentry/ownership suites,
first-refusal/cancellation tests. One short whole text/CSV/JSON source pair/control
at coherent retention; compound-key control if needed to establish algorithmic
benefit, no new framework or attribution against skipped-callback baseline.
Source/qualification/debts remain explicit; full platform/stress/installed delivery
batched after coherent correction. Dual-index actual-hash authority is an explicit
separate followup with a real stored-identity consumer, not silently removed.
