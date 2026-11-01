**B56 — controlled algorithmic removal of eager casing context (next bounded trial).**
Current source still maintains a preceding-cased flag and performs Sigma property
lookups at every rune in lower/capitalize/swapcase/title, although only lowering an
actual capital Sigma uses that context. B29/B43 identify this as material named
native work (113/1,059 and102/1,043 self samples), not a removable/causal fraction.
This is a coherent algorithmic deletion, not a universal value/frame rewrite,
property-table/cache replacement or local binder/reservation tweak.

Why revisit THIS algorithm: B36 never compared the declared original source—its
control was a stale rejected cursor binary. B54 now demonstrates9.78% false
cross-process effect for identical bits; B55 validates a0.81% identical-image
control with shared CoreLib/hashes. These are NEW source/method evidence, not
favorable B36 samples. Explicitly supersede B36's agent-written no-retiming rule
for ONE current-source controlled assessment of this algorithm only. Its original
records remain invalid/immutable; do not reuse their numbers or reopen B30/B33,
B51/B53 or other rejected prototypes. No algorithm tuning or second collection.

Before code, delete/replace:
- Remove four eager preceding-context variables and their per-rune updates.
  Pass original UTF-8 prefix/suffix into the existing rune mapper; only a capital
  Sigma being lowercased scans backward for preceding context. Keep the existing
  forward context, Unicode properties/expansions and title's separate word state.
- Reuse the existing safe previous-rune boundary helper and Rune.DecodeFromUtf8;
  no conversion/copy/index/cache, new owner, data representation or configuration.
- Preserve original-input context and Case_Ignorable precedence over Cased,
  including overlapping properties. Sigma is cased/non-ignorable, so backward/
  forward scans cannot cross another Sigma: each intervening ignorable run is
  visited at most once per direction, total linear work. Check current cancellation
  per visited rune; retain existing builder preflights, overlap and rollback.
- All supported Python results/identity contracts and managed safety remain. The
  tradeoff is occasional prefix decoding on Sigma-containing text, not an extra
  pass/copy for ordinary text. No public API/contract change or hidden cap.

Semantic acceptance: existing independent Greek/overlap/supplementary/long-interval
and all-four-method goldens, title boundaries/expansions, Unicode/ASCII casing,
finite-budget refusal and unpublished-output cleanup, cooperative cancellation,
retained aliases/results and budget reuse, both public APIs/actual target runtimes.
No implementation-only tests or checkpoint counting. New cases only for a real
coverage gap. Fresh nonincremental build, original-source audit and equal core/
worker/test/probe copies BEFORE freezing. No stale timestamp-preserving restore.

ONE <=45s owned same-process comparison, two maintained complete cases: text helpers
(affected) and CSV helpers (unaffected). For each case load original/candidate/
identical-original into separate managed contexts sharing CoreLib; hash witnesses
for actual input keys/words and full goldens/funding/cleanup must pass before timing.
128 interleaved warmup jobs/image,2s settle/case, seven rotating32-job batches/image,
ordinary GC/tiering. Keep every outlier; no external ratio/phase subtraction.
Retain only >=7.5% complete elapsed gain on text against BOTH original images,
<=3% CSV regression against both; original-control medians within3%, relative IQR
<=10% for every series and all elapsed batches>100ms. Full quiet/golden/balance/
exit gates mandatory. Source proof/deletion and semantics are independent gates.
If failed/inconclusive, remove candidate, preserve evidence/useful tests and stop;
no favorable retry, relaxed criteria or fragment salvage. Full Windows/Ubuntu,
original stress and Release package consumers only if retained at delivery.
