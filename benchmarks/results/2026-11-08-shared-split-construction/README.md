# Shared split construction — 2026-11-08

Explicit-separator split/rsplit reuse the existing whitespace range scratch,
complete output admission and final-array adoption. Delete three independent
builders, the temporary right-split list, reverse/conversion array and copied
list backing. Reuse the existing managed bytes search with current cancellation;
no conversion, new owner/pool/cache or public option. Whole unchanged strings can
remain aliases in the same funded owner; different owners receive copied storage.

Baseline committed94cb8963/core d6254b2e; candidatefa995d65 plus archived production
patch and explicit-context correction/core57e1ef6c. Complete production inventory/core hashes are in identity.json.
The earlier CPython checkpoint still describes94cb8963. Committedcbe88fef passes
independent Windows/Ubuntu actual11/10, originalStress and installed-core consumers
at [cached CI42477957586](delivery-ci.json). This does not qualify later sources.

| Complete delimiter report | Sync allocation | Async API allocation |
| --- | ---: | ---: |
| Forward explicit split |−5.47%|−5.47%|
| Reverse explicit split |−8.92%|−8.91%|

Each reads3072lines/35840B, aggregates words through an ordinary helper and writes
the complete report. Explicit separators require stripping newlines/skipping
empty fields; both candidate and baseline execute the identical complete source.
Local isolated CPython independently confirms full stdout/file goldens.
Existing allocation driver:32warmups, five rotated four-invocation samples, two
baseline controls/current core, normalGC/tiering. Captures public Run/RunAsync,
fixture reset, actual mediated file I/O, full checks and result cleanup; excludes
startup and compilation. I/O completes inline; this does not measure real pending
I/O latency. Whitespace/CSV/report controls are allocation-neutral within0.03%.
Initial/corrected desktop elapsed controls drift about2–20%, including adverse
report+24.6–28.7% and asyncCSV+15.7–26.6% observations; no
controlled elapsed/CPU gain claimed and no additional VM campaign performed.

150public/35internal focused Release checks per actual11/10 pass. Independent
Python cases preserve forward/reverse nonoverlap, Unicode/empty fields, zero/
negative/indexed maxsplit, exposed/keyword/expanded calls and search boundaries.
Ownership checks retain aliases after source-owner cleanup, repeat batch cleanup/
budget reuse and cancel during actual output admission before construction.
Original regressions allocate66752/67376B before refusing whole-result storage;
the retained implementation refuses before fragment allocation and leaves no
unpublished storage committed. First refusal identity/span and input contents hold.

All original failed conditions remain in observations.tar.xz, including the first
migration build and an invalid CPython immutable-identity oracle. The latter was
corrected because Python does not require identical allocation/identity reuse;
value semantics and independently funded aliases remain tested. This is a coherent
storage/admission simplification with measured allocation reduction. It does not
close the whitespace-helper performance gap or repay prior text/report debt.

[Allocation/raw elapsed](allocation-and-local.json), [identity](identity.json),
[status](status.json), [raw source/cores/logs/tests](observations.tar.xz).


The first shared-construction prototype2e232a4d failed two genuine suspended-count
tests: after awaited host I/O, explicit split/rsplit relied on thread-static ambient
context and returned despite cancellation. Both now receive the current context
explicitly; original whitespace and both explicit cases pass on actual11/10.
The corrected57e1ef6c allocation comparison reconfirms the table above. The
[original archive](prototype-observations.tar.xz), [identity](prototype-identity.json)
and [diagnostic](prototype-allocation-and-local.json) retain the earlier source,
conditions and allocation results. Neither its CPU nor the corrected desktop
elapsed is promoted to a controlled speedup claim.
