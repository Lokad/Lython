# R139 explicit lexical reads

Committeddf3bc92d/core345f versus working candidate. Existing complete main-wrapped
CSV/text/report helpers now read captured parser/skip marker, separator, newline.
Ordinary JSON/search remain controls. Fixtures/goldens unchanged and independently
verified; source hashes intentionally differ from R138. This is a same-script
source pair, not comparison across script shapes or CPython ratios.

Replace context-only closure reading with work-state cell reads, using existing
prefix/promotion and incoming cells. No new cell ownership, native proof, backend,
frame arena or cache. Keep local-cell construction/mirroring/special scopes and
writes frame-bound. Missing-cell/nonlocal fallback promotes before resolving.

Acceptance: unbound/deleted cells, live rebinding, writes, locals promotion,
retained captured returns after code disposal, pending captured protocols, first
refused admission/cancellation, funded aliases, awaited users/budget reuse and
managed depths. Focused actual11/10 then compile/warm allocation and diagnostic
elapsed with existing five-case driver. One quiet source pair if observations
justify it; preserve adverse controls and CPU uncertainty, no fixed threshold veto.
