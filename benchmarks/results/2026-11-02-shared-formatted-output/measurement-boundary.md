# B120 bounded source-pair comparison

Freeze exact 17511dbf/runtime93ee4513 baseline and uncommitted B120 candidate,
same benchmark host/auxiliary bits. Twelve standalone observations: two blocks of
report f-string warm/fresh, equivalent report via explicit format calls warm,
CSV/text/JSON helper controls warm. Existing checked workers/protocol; four-second
warmup per image, nine interleaved ~200ms batches. External/proc process CPU through
response, weighted means, runtime/JIT included. No guest-only CPU or allocation
claim from that protocol. Existing catalog/goldens; equivalent format-call report
independently verified with isolated CPython3.13.2 full stdout/file bytes.

One shared lease/quiet gates, owned300s service, cleanup joined. Startup excluded;
fresh includes compilation/disposal, warm excludes compilation. Staging, actual
mediated I/O, complete output/file goldens and previous-result disposal inside
batches. Last result/final script/process/fixture cleanup outside batches and joined.
Keep all controls/adverse conditions; refinement only when source/evidence justify.

Separately reuse the existing complete-job allocation driver, restricted to the
same two complete reports, original192 warmups/9x32 batches and isolated images
with shared CoreLib. Hash witnesses, goldens, admitted balance/retained-result
cleanup and final fixture removal required. Process-wide precise allocation
counter covers worker threads; its elapsed/CPU clocks remain diagnostic because
shared CoreLib/JIT conditioning differs from independent processes. Owned90s,
same quiet lease, no performance claim from primitive-only formatting.

Decision: source simplification, allocation and CPU/elapsed are separate. Preserve
single-field identity and report any standalone-format cost or control drift.
Permit one justified bounded correction/refinement; otherwise retain with explicit
debt or withdraw the changed runtime, keeping useful independent regression tests.
No fixed percentage veto or open retiming/profile campaign. Full qualification at
the retained boundary, publication deferred.
