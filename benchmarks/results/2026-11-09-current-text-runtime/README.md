# Current whole-text runtime attribution (R166)

One checked full word-count/report script, 2026-11-09, quiet F4as_v7 Ubuntu24.04.4,
.NET11RC3, ordinary GC/tiering plus explicit perf-map diagnostic setting. Reuse
the R164/R165 final precommit freeze (core80a3540d), production-source equivalent
to committed96e534f9, with unchanged persistent worker/adapter/catalog. Exact code
and source identities are retained; not a fresh postcommit build or CPython timing.

Existing perf/worker protocol:128-job warmup requests/64-job capture requests,
six-second target, unchanged20s frame guard, finite120s owner plus30s stop/margin.
One usable **5.856 execution-clock seconds,5.77 guest CPU seconds,1147 guest samples**.
All1222 samples include34 tiering and41 caller/other;70 guest leaf IPs unknown(6.10%).
Whole batches include reset/protocol/validation; guest/caller groups stay separate.
This is CPU attribution, not an elapsed before/after or a removable-work estimate.

| Innermost observed guest phase | Samples | Share |
| --- | ---: | ---: |
| VM prefix, including useful inlined work |444|38.71%|
| Text kernels/construction |268|23.37%|
| Dictionary operations |96|8.37%|
| Scalar operators |73|6.36%|
| Native call preparation/body |71|6.19%|
| Memory admission |62|5.41%|
| Operand/result publication |27|2.35%|

Text leaves include51 whitespace collection,33 UTF-8 casing writer,26 split-core
and16 managed ASCII casing samples. These counts include useful work and do not
imply all23.37% can disappear. Ordinary binding/frame construction has no separately
observed phase here: the existing native helper-return route handles this helper;
that does not establish universal zero call cost. Dictionary samples include34
native get and30 contextual hash preparation; cached string hashing already exists.

Current source rejects several obsolete premises: ASCII case already uses managed
bulk intrinsics; lower/split writes final independently owned words; UTF-8 completion
does not recount scalars; CSV parser bulk-scans and transfers funded row arrays;
binding targets final slots with inline presence/reused workspace/deferred contexts.
Reservation commits update local balances; they do not resize the host per fragment.
The standalone allowance already shares one gate, not a lock chain per scope.

Next bounded design phase considers complete native text construction and dictionary
query/update pipelines. A prototype must identify actual work/storage displaced,
preserve Python Unicode/context, identity/alias ownership, mutable protocol/evaluation
order, cancellation/refusal and cleanup, and compare complete text with CSV/config
controls. No new frame arena, repeated-hash cache, universal tagged-value commitment
or narrow counting idiom is selected from this capture. Larger redesigns remain
possible; original slower call-arena/control evidence is not a permanent veto.

Initial space checks refused before mutation (53,248B short including payload).
One additional exact tracked B501 archive joined the existing B514/B507 selection.
Fresh idle lease, exact SHA/single aliases/metadata/parents/receipts precede removing
only those redundant remote compressed copies. Their canonical Git copies, raw
trees, receipts and frozen workers survive;240MiB OS/12MiB evidence floors unchanged.
Raw/maps archived under the owner; full golden/quiet/Closed/join/map-retirement/
RAM-unmount/localSHA gates pass. No VM users remain; terminal state cached.
No runtime change or repeated unchanged correctness qualification in this milestone.
Full R164/R165 delivery is tracked separately; publication postponed.

[Summary](summary.json), [full leaf/phase attribution](attribution.json),
[source gate](source-gate.json), [identities](identity.json), [source](core-source.json),
[raw/core/maps](observations.tgz), [preparation/refusal](preparation.tar.xz),
[space proof](space-recovery.json), [terminal](terminal-status.json).
