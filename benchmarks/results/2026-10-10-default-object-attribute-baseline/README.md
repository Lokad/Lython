# Default-object attribute compatibility baseline

Corrected member-assignment order and asynchronous target/slot handling before
the next performance experiment. **33 independent checks** expose **nine runtime
failures** on original production. The fixed baseline and isolated candidate each
pass **154 selected checks**, **9,368 Debug tests** (1,547 white / 7,821 public),
and **58 trusted CPython comparisons**. Matching Debug probes are built first.
No performance collection or full lane runs in this assessment.

## Original-source evidence

Original corrected-fixture source `218f34a33f50f9f5e53ff931037f92ff9d932758` has production
`7fe2dd6ab6f53578d03dc94564110d97f5912840` and tests `0a463a1c1aad66197fb5563784a4bd1bd9a70057`, identical in tests
to the fixed source and candidate. The preserved receipts show **63 white passes
and 82 public passes / nine failures**, before comparing any optimization.
All nine pure snippets pass CPython; seven match Lython in each public mode.
Guest set/delete slot descriptors fail binding, and a supported property receiver
proves the member-assignment order discrepancy at runtime.

Initial fixtures retain **19 passes / three failures of 22** and expanded fixtures
**143 passes / 11 failures of 154**, with original source and versioned assemblies.
The first ordering fixture used unsupported call-result target syntax and did not
reach its intended assertion. The property deletion fixture used an unsupported
constructor form; it now uses the supported `property.deleter(...)` derivative.
The corrected tests run against isolated original production, where deletion
controls pass and the ordering assertion fails at runtime. A local compiler log
also retains an accidentally duplicated existing async deleter; the duplicate was
removed before successful gates. No fixture failure is erased or claimed as a
runtime optimization.

## Compatibility fixes

Commit fbc26d18 evaluates member-assignment values before their receivers in
lowered sync/async and the syntax interpreter. Lowered async member assignment
awaits its setter. Chained and unpacked assignments use the existing async target
stores, with target reads lowered once and snapshots kept reserved through stores.
Evaluation order, aliasing and original target spans remain covered.

Commit b724d4ce binds guest descriptors used as `__setattr__` / `__delattr__` and
awaits descriptor binding on async assignment/deletion. Existing engine bindable
slots retain their binding behavior, and callable validation stays after binding.
The default async deleter already existed and remains unchanged.

The ten white checks pin slot growth at **64 bytes plus existing tracking costs**,
free overwrites, exact denial reservation/span/rollback, live values, actual
descriptor context/receiver/span, descriptor charge/order, checkpoint counts,
fuel-before-cancellation and clean depths/reservations. The 23 public checks cover
data/non-data/property precedence, inherited/live overrides, `__getattr__`,
explicit slot metadata/errors, closures/generators/super, custom slot descriptors,
RHS-before-receiver effects, all six assignment forms, genuine host suspension,
overlapping independent runs and cancellation/reuse. These are meaningful
independent regressions, with the original 121 selected controls retained.

Fixed source `3555caf98f481b8f1092a979a1c4f0c800042687` / production `ea4886034b01717ee5b83ceb0207a4b974adff86` has
tests `0a463a1c1aad66197fb5563784a4bd1bd9a70057` and unchanged whole benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.
Original failures, completed full suites, all probes and source-versioned binaries
are retained privately and referenced by hashes in [evidence](evidence.json).

## Isolated performance candidate

Candidate `122384e3099cf25fd162694d9273abbce1f2fed5` / production `d5165bbb6cb51aac155234a30e4df91ff8011b96`
remains on `perf/default-object-attributes`, pending separately declared timing.
Its four-file runtime diff recognizes only the exact sealed built-in get/set
slots and routes instance accesses to shared descriptor-aware handlers. It avoids
their internal bound wrapper/argument preparation while preserving live MRO
lookup, custom slot binding, `__getattr__`, source/services, descriptor calls,
logical fees, slot/ownership growth tracking and real async suspension. Explicit
slot invocation uses the same handlers. No new method factory, pool, checkpoint
change, per-type byte prediction or measured gain is claimed.

Both full Debug gates and 58 CPython comparisons pass. A receipt-count correction
from 56 to 58 precedes timing; the frozen acceptance thresholds and decision code
are unchanged. Before timing, use fresh matching Release producers from the
corrected exact-head green baseline, selected Release gates and seven complete
canonical CPython jobs. Earlier producer manifests are not identical.

The next experiment declares **fourteen seconds-long paired micros** with a
**30-second external process-group cap**, keeping the owned-method target and
empty, integer-loop, positional/keyword, complete sort/output and ASCII-pipeline
controls. Require method gains beyond max(2%, retained spread) in both passes,
with no repeated material control time regression or allocation growth above
0.5%. Separate ordinary allocation and native-code diagnostics follow normal
timing, retaining every observation/tier/inline summary/target. No favorable
recollection, full lane or qualified Python multiplier. Longer comparison
qualification waits for an infrequent declared milestone.

## Additional subset findings

Separate minimal probes in both modes confirm two pending findings: assignment
to `receiver().value` is statically rejected with LA1068, and the fuller
`property(fget, fset, fdel, doc)` constructor is rejected as too many positional
arguments. CPython runs both. These are explicit PLAN.md follow-ups, separate
from the successful supported-receiver and property-deleter controls above.

The prior synchronous-dispatch, method-receiver-lease and unused-super-anchor
candidates remain rejected. Executable class-method qualification, original
CSV/pipeline/C05 causes, milestone/secondary comparisons and external Utf8Regex
integration remain open. This assessment claims no performance integration.
