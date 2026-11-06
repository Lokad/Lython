# Q01 one contextual hash-preparation walk

Source62d2c9ca, production1134d6e8. P02/B324 complete-text capture6.77guestCPU/
1346samples:251prefix uniquely mapped, dictionary hash/get/set/classification
cluster material but includes useful work; no removable percentage/CPU prediction.
B322/B323 setup failures precede samples, all conditions retained. B324 owns all
joined workers/profilers, quiet gates, Closed/final zero and removed fixtures.

Independent public probes (sync and async) demonstrate tuple prevalidation skips
an earlier __hash__ callback for dict store/get/contains/set.add, unlike CPython;
an earlier callback ValueError is replaced by later TypeError. Public hash(key)
already preserves that order. No compiler diagnostics involved.

Selected coherent change: contextual hash preparation returns normalized hash and
protocol requirement in one semantic traversal. Retire recursive ValidateTupleKey
and separate classification/hash walks on contextual dictionary paths. Keep only
coarse top-level key shape rejection where callers need pre-allocation validation;
no recursive preflight of later tuple elements before earlier hash callbacks.
Existing cycle/depth guards remain on the actual hash walk, with current token and
first memory refusal checks. No cache, side index, scalar ledger, second dictionary
format, new public API/options, budget subsystem or opaque callback reordering.

Preserve native/stored hash normalization, bool/int/float equality, tuple-like and
slice values, actual failing nested value, identities, reentrant mutation, exception
order, cancellation, refused admission, retained aliases, awaited cleanup and known
hash copy paths without callback replay. Raw context-free consumers/async selection
can still need classification; do not leave unused compatibility hooks. Existing
asynchronous hashing limitations are not silently claimed fixed: separately qualify
actual suspending __hash__, then plan the required continuation/cycle-owner work.

Focused Release hash/container/managed-depth/refusal/ownership tests on both actual
runtimes and independent new Python controls first. Bounded existing complete
compound-key aggregation2/8 plus whole text/CSV controls, ~120s/owned160s, normal
GC/tiering/current-control/full golden/quiet/lease/join/funding/fixture gates. No
new framework or whole-heap audit. Preserve adverse/uncertain outcomes; structural
and correctness gains are distinct from allocation and measured CPU. Full platform/
stress/installed consumers only at coherent delivery. Publication postponed.
