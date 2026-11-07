# Returned-record CSV workload / current attribution — R69/B422

2026-11-07. New maintained csv-validated-records workload: five CSV records/block,
keyword-only default supplied by an ordinary keyword call, negative/explicit-skip
early exits, tuple return/unpack, aggregation and complete report. Independent CLR
goldens retain three accepted records/block. Empty/small/large inputs pass all
75public file-worker checks per actual.NET11/10; async large/fresh small included.
Supervised CPython3.13.2/Lython verification passes all3cases and separate
compile/warm/compile-to-result operations. These are correctness/smoke clocks,
not qualified ratios. Initial wrong Python-worker path fails before worker launch;
the corrected existing path and original condition are retained.

Production824fc6bb is unchanged/fully qualified in CI42418518578. Attribution
reuses frozen15c04e2a plus unchanged7cf37ca5 manifest worker/normal0.4adapter;
new source/fixtures/goldens arrive through the existing validated manifest. The
benchmark-only patch/catalog are explicit. No producer or core change.

One large synchronous current capture on the quiet F4as_v7 VM:6s guest warmup
in32invocation batches,2s settle,6s wall199Hz CPU-clock/FP. PerfMap3 and five
declared JIT method names are diagnostic overrides, not normal timing controls.
**1,121 guest samples /5.64guest CPU seconds**,108unknown leaves(9.63%), no
malformed map lines or empty/single-frame guest chains;5.670s accounted job elapsed.
All identity/golden/quiet/Closed/exit/join/empty-fixture gates pass. Archive verified
under lease and independently locally; all users finish and RAM mount is removed.
120sowner+30stop+30margin, no restart; staging326,131,712B passes without reclaim.

Instruction prefix119self/input45; BindInto22, InvokeBound20, ordinary call14,
binding entry9 and rent6 show actual activation work, but no dominant removable
frame cost is established. Inclusive InvokeBound431 includes useful guest work;
do not add overlapping counts or call them saved CPU. No speedup claim.

The capture also locates21Monitor enter/exit leaves in DrainLeased underneath
MaterializeUnpackingSequence (plus its reservation/iteration work). Source confirms
an exact immutable tuple is copied into a temporary List through checked iterator
wrappers, with shell/capacity reservation, before ordinary assignment can unpack
it. Select this complete per-record transport phase first: borrow immutable native
tuple items through all unpack consumers, keep snapshots for mutable/protocol
inputs and keep funded ownership through real target suspension/cleanup. Reuse
the existing materializer for the latter. No general iteration or memory framework.
Do not predict a percentage from sampled locks. The larger keyword/variadic
activation consolidation remains a separate roadmap item requiring its own
genuine pending-default semantics and before/after decision.

[Raw source/profile/JIT/unknowns](observations.tgz), [receipt](archive-receipt.json),
[all leaf/inclusive chains](guest-call-chains.json), [source identity](identity.json),
[catalog](catalog.json), [independent CPython smoke](cpython-correctness.json),
[focused checks](focused.json), [contract](contract.md), [terminal status](terminal-status.json).
B422 is terminal/archived/verified; never query it again. Publication postponed.
