# Conservative live deque storage, 2026-10-12

Deque containment now uses one fixed **128-byte charge per live node**, plus
its existing 128-byte shell. Node storage includes small retained-value headroom
for every type. Large payloads keep their separate owners. This removes **138
net production lines**: the scalar identity/refcount map, adoption/unadoption
helpers, inverse rollback loops and duplicate snapshot refreshes.

Full bounded eviction and rotation reuse actual linked nodes. Indexed replacement
transfers funded storage. Growth still preflights allocation; failed private
construction refunds its storage, while streaming extension keeps its inserted
prefix. First attachment funds existing storage before publishing the governor.
Clear, pop, removal and dropped-container reclamation release live charges.
Host mediation, cancellation, stack guards, cycles and public projection remain.
Other coupon users and the forced-GC dependency remain separate work.

The estimate deliberately overcharges aliases and nonnumeric values. A deque of
10,000 references to one integer now needs 1,280,000 bytes of node allowance,
instead of 640,000 bytes plus one scalar coupon. Python identity is preserved;
tight-budget acceptance and reported peaks can change. Distinct unowned small
integers retain their former combined node-plus-coupon estimate. SPEC §14
documents the contract.

## Validation

Original production `3248a5bd4931c6313c49575579ec39144f1e5e46`, diagnostic source
`7935168d203ca21c49d3cca09fef5eee50e4dc1d`, gives 11 failures and four passes in
15 unchanged focused checks. These include deliberate fixed-node policy
differences as well as demonstrated defects: failed attachment publishes a
governor and strands its shell charge; existing numeric values are unfunded on
attachment; saturated-budget eviction/replacement requires additional coupons;
rotation replaces actual nodes. The original DLL, hashes and outcomes are frozen.

Qualified root source `24a6afdaa385a0f0b92d542686848258d8436929`, production
`58f504f52465e6c86c55cf54fde7ebf19f28bca7`, passes **10,173 complete Debug checks**
(1,995 white-box, 8,178 public), **201 focused Windows Release checks**, and
**67 focused Linux Release checks**. Tests cover both-end eviction, node identity,
growth denial, constructor refunds, attachment denial/retry, release/drop
reconciliation, bounded scans, cycles and escaped large integer payloads through
genuine host suspension. Existing 100,000-item bounded streaming checks pass.

The first candidate retains three failures against old 64-byte reference-node
oracles and the old alias peak oracle (actual peak 1,280,992 bytes). Fee oracles
were updated for the declared contract; the execution budgets remain unchanged.
Those initial binaries were built before the production commit, from its parent
plus the production edit, and are not the qualified final producer. Test-only
analyzer/result-type build errors are retained separately. Final root binaries
are hashed independently.

## Short comparative measurements

Eight fixed collections cover two passes of four cases, each taking 7–8 seconds
including cleanup. Ordinary separate workers use .NET 10.0.12 and CPython
3.13.16; guards, tiering and GC settings remain normal. The supervisor's tiering
override is stripped before worker launch. There are 337 verification, warmup,
calibration and measurement attempts, including 168 measured samples. No timing
retry or ten-minute lane ran.

The frozen baseline is the preceding qualified producer
`3eca95246b153115f878dc96b9176473a0c4fcf8`, with the same production tree as
`9ff0da85`. Candidate workers are built from `24a6afda`; actual library/worker
hashes, source identities, outputs and termination receipts are retained.

| Case | Pass 1 change | Pass 2 change |
| --- | ---: | ---: |
| Empty control | +0.12% | −0.61% |
| Integer loop, 16,384 iterations | −1.31% | +1.62% |
| CSV discard, 128 rows | +4.25% | −5.14% |
| Bounded deque, 1,024 appends | −25.00% | −35.20% |

Deque medians move **200.89 → 150.67 µs** and **224.32 → 145.35 µs**;
corresponding CPython medians are 33.60 and 36.46 µs. This supports a targeted
short-round improvement, without certified milestone ratios or a broad speedup
claim. The slower second basic-loop control has low spread (0.65%/0.89% IQR);
retain that signal. CSV has broad spread and opposite directions. Actual node
identity checks prove reuse, but allocated-byte savings were not measured.

All owned services and worker processes are stopped, groups are empty, sources
are clean, assembly hashes reconcile and the VM lease is free. An initial
cleanup audit tried stopping an already-unloaded transient unit; the corrected
audit handles that state. An initial archive was written inside its input
directory; the final archive is outside it. Neither error reran or discarded a
measurement. Raw outcomes, timing samples, failures and final cleanup proof are
in [evidence.json](evidence.json).
