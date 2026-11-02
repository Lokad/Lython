# B87 existing abstract-value cache — before code

B86 passive native stacks identify37 distinct syntax/source-span hash paths;
all37 pass through AbstractState cache lookup/insertion. This is actual cost
evidence, not an exact removable CPU fraction. Source confirms record keys
recursively hash structure although values belong to a particular immutable
syntax node within one mutable binding state. Every invalidation already clears
the cache, making its version counter and per-entry version wrapper redundant.

Hypothesis: use node identity keys in the EXISTING cache and retain just the
clear-on-mutation invariant. Delete recursive record key hashing, _version,
CachedAbstractValue wrapper and redundant version comparison. Store the existing
AbstractValueResolution directly. Both constructors use the same comparer.
No new cache/layer, analysis skip, AST mutation, engine/value rewrite, owner,
public option, new dependency, unsafe code or runtime quota.

Semantic acceptance: preserve resolved/unresolved caching, Set/Remove/sequence/
container invalidation, Clone/Replace/Merge/captured-state isolation, Python-shaped
diagnostics and ordering, regex static binding/effective flags/current tokens,
module/closure/generator semantics. Distinct but structurally equal nodes can now
be independently resolved; cache hits are an implementation detail. Keep the
same immutable AST spans, not a global identity ledger. Existing coarse compile
preflight funds working/surviving storage; no unfunded retained aliases or owner
migration. Existing cancellation stage checks/first-origin refusal/compiled
owners and cleanup stay. No finer post-parse interruption promise.

Independent inference regressions exercise real resolver state mutations/clones/
branch joins without asserting internal comparer/version architecture. Existing
focused Release frontend, annotations, directives, comprehensions, regex binding,
compiler cancellation/admission/retained program controls on actual.NET11/10.
If semantic/source deletion gates fail, stop before timing.

ONE<=45s VM complete before/candidate/identical-before comparison using existing
private ALC driver, shared CoreLib/hash witnesses/identical ordinary0.4 auxiliary
bits, normal GC/tiering, no forcedGC. Four unchanged cases: compile CSV-helper/
text-helper.large, fresh config-edit.small, warm text-helper.large.1024 compile
warmups/image,128 execution warmups;2s settle/case;7 rotating batches;2048 compile
jobs,32 fresh config jobs,32 warm text jobs. These sizes use B83's actual complete
job costs and avoid its excessive512 fresh-job batch; B83 is never retimed.
Compile includes validity/compiled-owner disposal; fresh includes compile/full
public Run/mediated I/O/reset/output+file goldens/result+compiled disposal; warm
excludes compile. Startup excluded. No external comparison or predicted speedup.

Retain >=10% elapsed compile gain in BOTH compile cases versus BOTH originals,
execution regressions/original drift<=3%, inclusive-linear IQR<=10%, batches>50ms.
Every source/hash/isolation/full golden/balance/final0/natural fixture removal/
quiet gate passes. One capture, no retiming/fragments/threshold relaxation.
Otherwise withdraw entire cache prototype, fresh nonincremental rebuild and
focused tests; keep independent semantic regressions/adverse evidence. Full
Windows/Ubuntu ordinaryboth, originalStressboth, Release package consumers only
if a runtime change is retained. No publication/tag/adapter package.
