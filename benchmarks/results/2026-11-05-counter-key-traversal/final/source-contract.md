# E3a Counter key-set removal — before code

Baseline f66427f1/frozen793586f5 full4168internal/16033public per actual11/10 pass;
CI42113971859 active, earlier terminal runs cached. No new full qualification yet.

Three isolatedCPython3.13.2/public Lython mismatches demonstrated and preserved:
Counter equality/ordering omit the second source's comparison effects; binary
operations mishandle equal custom keys because the raw seen HashSet uses structural
identity rather than Python hashing/equality; equality clearing its source omits
Python's dictionary-mutation error. Source of CPython3.13 collections confirms
comparison passes over both counters and dictionary membership in binary passes.

Delete four raw HashSets, three UnionWith operations and seen-key publication.
Equality/ordering traverse each source in order; binary results query the original
left dictionary for right-only keys. Use existing explicit context on async lookups
and check cancellation/refusal after actual value callbacks. Preserve supported
sync-only key callbacks, real async value comparison, insertion/evaluation order,
missing-as-zero semantics, normal exception propagation and cooperative cancellation.

Each live enumeration can retain old table backing when a callback clears/grows
its source. Before its callbacks, reserve the source's existing CommittedStorageBytes
through the active governor; keep that temporary hold through all awaits and dispose
only after the enumerator. Reuse current scopes, no new ownership infrastructure.
Take each pass's estimate when that pass begins. This is conservative: a source
can require roughly another backing's worth of shared allowance even when a specific
comparison does not mutate it. Earlier denial is intentional/documented; no fallback
or retry on refusal. No per-object ledger, tracing, hidden cap or new policy.

This removes one finite dependency on structural instance hash/equality, not the
whole E3 storage coupling. Mapping/view/public-compound hash callers still require
proof before an identity/backing migration. Do not change the global comparer here.

Acceptance: independent public goldens for effects/custom keys/mutation, existing
Counter/dict/protocol suites; real async value equality clears inputs then waits,
with old backing still funded and awaited cleanup; first refusal/cancellation,
unchanged targets where denial precedes work and reusable shared budget. Focused
Release during iteration; one short correct-native Counter plus complete-script
control comparison at coherent retention. Full platform/Stress/installed checks
batch later; current f664 delivery remains separate. No CPU/allocation claim without
measurement, no timing against newly corrected observable callback behavior.

## Corrections discovered during implementation

The original combined equality/ordering probe failed frontend LA3143; it did not
by itself prove the equality event mismatch. The preserved equality-only baseline
probe confirms oneeq versus Python twoeq. Ordering additionally required correcting
frontend admission AND runtime dispatch: existing Counter dunders were unreachable
from ordinary operators. Greater ordering now uses original operand/iteration order
and >= protocols, rather than reversing the sources and applying <=. Async value
comparison/truth uses the actual async evaluator; dict-key callbacks remain sync-only.
The first custom-key binary prototype also lacked ambient context in its out-of-line
callee; preserve that failed run, and establish the existing active-context scope.
Five independent public CPython goldens now pass; focused186public/104internal per
actual11/10, including six true suspension/ownership cases, passes. Previous source
f66427f1 fully qualified42113971859 GREEN/all8; this Counter source remains focused.
