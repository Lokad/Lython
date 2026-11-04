# Shared UTF8 search windows

Before collection2026-11-04.65e0871d/frozen7cf2623c vs archived4-file patch,
59 net production lines removed. One existing StringSearchBounds now carries
byte bounds/validity, shared by all ordinary/native/bound affix and search calls.
Default windows require no scalar count; explicit bounds normalize once after
conversion. Preserve actual scalar indexes, empty ranges, tuple validation and
hooks; reject beyond-length empty searches and invalid start before end hooks.
Existing shared cooperative checkpoint also covers repeated count matches.
No new dispatch/cache/arity family/option/owner or skipped required admission.

Two public CPython regressions RED on frozen7cf2623c, GREEN on both targets.
Other focused string/bounds/real-suspension/cancellation/admission/retention checks
before timing. Exact images/auxiliaries/CoreLib/hash witnesses required.

One7-case collection: complete ASCII/Unicode1KiB-line prefix/count filters of
256KiB files; existing CSV/text warm, report/config fresh, CSV compilation.
Same previous private3-image driver except frozen-case guard,6sec guest warmup/
image, alternating preparation,2sec settle,9rotated200ms baseline-sized batches.
Normal GC/tiering, CPython3.13.16 complete goldens, baseline/final-zero owners,
fixtures/quiet gates. Actual mediated RAM I/O, output checks and result cleanup;
fixture staging/reset/startup/hash/final cache teardown excluded. Compile separate.
No new CPython ratio or broad CPU claim. Preserve all adverse observations.

Size300sec owned limit using prior213sec six-case timing including fixture costs.
Persistent evidence, RAM fixtures only. Decide deletion, allocation and elapsed
separately and explicitly resolve any representative regression debt. Confirmation
only for materially inconclusive evidence; no fixed-percent veto. Batch full
platform/Stress/installed consumers at this coherent runtime delivery boundary.
