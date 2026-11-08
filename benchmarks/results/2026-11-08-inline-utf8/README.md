# Direct token storage — R76 retained

2026-11-08. Measured baseline core3efc0895 (runtime06651b11); final core903eb346
is c384956f plus the archived complete R76 source inventory. Informational
SourceLink alone does not establish source equivalence. Production38f53c33 passes
full Windows/Ubuntu actual.NET11/10 Release, original Stress and installed-package
checks: [cached CI42439532114](delivery-ci.json). Publication postponed.

Copied short text can hold up to8 UTF-8 bytes in compiler-managed word storage.
Casing/split and CSV factories write directly into reserved final capacity, then
commit before publication. Adopted arrays keep their full backing and capacity.
Native span/byte-length readers avoid materializing a memory view. A retained
ReadOnlyMemory borrow obtains one stable tiny array, prepaid by existing128B
metadata headroom. No new budget/owner/ledger/pool, tracing/GC, token cache, unsafe
code/API, layout or endian assumption, native probing or runtime dependency.

The existing five-complete-script diagnostic passes all goldens, root balances,
image/hash isolation and owned-fixture cleanup under both APIs. Final allocation:
text **−9.43–9.44%**, CSV **−9.03% sync/−8.03% async**, report about−1%; numeric
controls about−0.05%. This includes general wrapper costs and all cache effects,
not merely removed payload arrays. Desktop elapsed remains diagnostic and includes
original adverse synchronous text, CSV and async-float observations.

## Controlled comparison

Quiet Azure F4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering. Existing worker,
normal optional0.4 adapter and unchanged scripts/fixtures/goldens. Two old/two
current processes;5s warmup/role/job,7rotated150ms rounds. Positive means slower;
ranges include every new/old process pairing, not confidence intervals.

| Complete script | Final elapsed | Old / current spread | Interpretation |
| --- | ---: | ---: | --- |
| Text helper, warm |−12.67to+0.04%|11.59 /2.59%|Inconclusive unchanged control|
| Text helper, warm-async |−3.27to−2.32%|0.48 /0.49%|Modest affected improvement|
| Ordinary CSV helper, warm |−2.03to+0.51%|2.07 /0.47%|Near baseline/variable|
| JSON-to-Markdown report, warm |−3.33to−0.29%|1.69 /1.44%|Modest elapsed benefit|
| Float report, warm-async |+0.88to+2.66%|1.76 /0.01%|Accepted adverse debt|

Retain the completed storage design for stable allocation savings, fewer temporary
payloads and modest affected elapsed gains. Explicitly accept the small async-float
debt; unrelated improvements do not explain it. Quantized process CPU is mixed;
no broad CPU, CPython multiplier, embedding-tail, RSS or startup gain is claimed.
Warm includes invocation, mediated RAM I/O, full validation and prior-result cleanup;
compile/startup/staging/reset/protocol/final cached teardown are excluded. Async
timing uses completed I/O; genuine suspension is separately correctness-tested.

Final B436 completes five jobs in141.346s; initial B435 four in114.465s. Both pass
source/core/auxiliary/actualReady/runtime/golden/quiet/Closed/exit/join/fixture gates,
archive verification under lease and locally. Initial092 includes adverse sync
text+0.58–5.35% and CSV+0.75–3.14%. Intermediate7eb/5b observations remain raw.
Tier1 hash code already inlines the getter; no inlining attribute or causal claim.
Refinement removes added stack copies and caller storage branches, keeps prepaid
views available to native cleanup, and separates cold cache construction.

Focused Release162internal/415public checks pass per actual11/10. New cases cover
concurrent stable borrows, capacity/result funding, cancellation/refusal and reuse;
existing Unicode, regex offsets, delayed I/O and terminal-refusal coverage remains.
Initial two refusal-inspection failures and original source are preserved. The
fixture now holds a borrowed view before refusal and checks its surviving data;
the final pure data getter also preserves native cleanup access.

Persistent evidence stays outside owned128MiB RAM fixtures. Setup reserves payload
plus evidence headroom and240MiB OS free space; earlier R75's256MiB failures remain
unchanged. Only archive-verified redundant extracts/two092 core links were retired;
all original bytes remain in immutable VM and independent local archives. No live
collection remains. See the [assessment](assessment.json) and [raw evidence](observations.tar.xz)
for all source states, binaries, diagnostics, contracts, adverse data and receipts.
