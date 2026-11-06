# Common UTF-8 word discovery — 2026-11-06

Retain the refined R03 shared planner as a bounded simplification, without a
demonstrated warm-script speedup. Ordinary and cased forward splitting use one
word/capacity walk; reverse shares direct ASCII category-run discovery. Remove
CollectCasedWhitespace and ASCII Rune decoding. Unicode scalars retain existing
decoding, Python's29 whitespace values and case-capacity handling. Existing
64-byte cancellation chunks, all-output admission, independent fragment ownership,
custom methods and cleanup remain. No new representation/cache/owner/policy.

The first SearchValues prototype is withdrawn; both search tables, metadata
construction and library search dispatch are removed. Its adverse result remains
fully preserved. Direct byte classification is the only retained implementation.

## Complete-script observations

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3/CPython3.13.16, unchanged b94 worker/protocol2,
normal GC/tiering. Two identical current roles, production794e552b/reference4011
and CPython. Five warmup seconds/role; seven rotated150ms rounds. Each collection
has three jobs (~86seconds),130second owner and all full-golden/quiet/Closed/final
funding/worker-join/fixture-removal gates passing. Raw archives are hash-verified.

| Variant / whole script | Candidate / identical control | Production reference | Changes versus reference |
| --- | ---: | ---: | ---: |
| B332 native search, warm text |2.356 /2.333ms|2.272ms|+3.69 /+2.69%|
| B332 native search, fresh text |2.604 /2.440ms|2.406ms|+8.21 /+1.38%;6.31% role spread|
| B333 direct bytes, warm text |2.344 /2.286ms|2.308ms|+1.59 /-0.93%;2.48% role spread|
| B333 direct bytes, fresh text |2.379 /2.392ms|2.413ms|-1.39 /-0.85%|
| B333 unchanged warm CSV |4.692 /4.714ms|4.693ms|-0.01 /+0.45%|

The original adverse warm result justified a bounded simpler refinement; native
lookup overhead on short words is a hypothesis, not a proven cause. Refined warm
roles straddle the reference: inconclusive, not a CPU gain. Fresh improvement is
small/diagnostic (current IQR up to1.14%); CSV is roughly flat. Retain for common
discovery and deleted per-scalar machinery, with warm uncertainty explicit.
No allocation percentage, broad CPU/RSS/embedding-latency claim or primary-table
refresh. Do not compound deltas or erase B331 fresh CSV/text regression debt.

Warm includes execution, mediated RAM I/O, full output/file validation and
prior-result cleanup. Fresh adds compile/disposal. Startup/staging/reset/transport/
final teardown excluded. CPython imports persist; Lython isolates imports.

[Initial source/contract/raw data](initial/) and [refinement](refined/) preserve
both variants. The initial setup stopped before service launch at a disk-headroom
check. Only the hash-verified redundant transferred payload was removed; its
local copy remained. A second check still refused, then free space recovered and
all source/worker hashes were reverified before the sole B332 launch. Exact
failed conditions and launch headroom remain in setup-correction.json and
launch-disk-check.json. No failed setup produced a timing observation.

## Correctness / delivery

Initial focused315 internal/193 public and refined69 internal/177 public pass
per actual.NET11/10. Refined selection includes the changed whitespace/casing,
preflight, retained-fragment ownership, cancellation and actual async custom-method
controls. Six independent isolated CPython goldens cover63/64/65 and4095/4096/4097
byte/scalar transitions, NUL/DEL, complete ASCII inventory, maxsplit/reverse,
Greek final sigma and expanding dotted-I/ligatures. Original stdout CRLF is
preserved; only the independent harness newline comparison was normalized.
No runtime fix followed from that Windows harness mismatch.

Combined R03/R05 full platform/originalStress/installed qualification passes on
51db4209; the exact terminal receipt below excludes later Q02. Publication/tagging postponed.
Whole-script objective remains active.

Combined R03/R05 source51db4209 is committed/pushed. Full local.NET11 passes
4,215 internal/16,112 public; both actual.NET11/10 installed Release consumers
pass. [Exact validation scope](../2026-11-06-builtin-class-patterns/validation.json).
Cached terminal CI42174873217 is GREEN/all eight Windows/Ubuntu, both-runtime,
originalStress and installed-package gates on that source.
[Exact receipt](../2026-11-06-builtin-class-patterns/delivery-ci.json).
Never query the closed run again. This excludes the later Q02 prototype.
