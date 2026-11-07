# Actual executable declaration bindings — R49

2026-11-07, baseline qualified8c9fd0d7/frozen d3a6c778; candidate b18758e7 plus
the preserved five-production-file patch, precommit metadata b18758e7. Exact
checked source/core/worker identities are retained. No new dependency/backend,
execution route, opcode, value cache, owner framework or resource policy.

The executable compiler registers actual module function/import/class/type-alias
declarations in its existing slot layout. Ordinary loads then use those slots;
live namespace writes and deletion keep the existing bridge. Function scopes
already have complete lexical layouts. Module declarations remain published to
their backing namespace after frame cleanup. Shared import binding-name facts
replace duplicated ordinary/from-import branches. The compiler does not eagerly
predeclare every dead module binding: a return followed by unused declarations
creates no slots in the compile-only census. Existing dynamic name/global and
builtin-before-binding behavior remains.

Eight new cases have isolated CPython3.13.2 and preceding-runtime goldens: failed
function/class declarations, namespace rebinding/deletion, global replacement,
import aliases, lazy aliases, stable key iteration and call-target snapshots
through argument effects. Both APIs and repeated shared-budget cleanup pass.
Focused1008internal/2819public checks per actual.NET11/10 pass, including genuine
suspension, cancellation, first refusal, imports, closures and namespace lifetimes.
The initial prototype passes its seven new cases but fails29existing cross-frame
tests: AssignExecutableBoundName switched declarations to slots without retaining
their namespace values. The runtime publication condition is corrected, not the
independent assertions. This also corrects R48's earlier StoreName source-caption
assumption. Exact failures/initial source and corrected snapshots remain.
Unused quota-era checkpoint vectors in OrdinaryFunctionTransportTests are deleted;
they had no consumers/assertions. All body, input identity and unwind checks stay.

The existing five-case reflection driver runs unchanged: search/text affected and
ordinary CSV/JSON/report checks,32warmups/image, five rotated rounds*4jobs,
before/current/identical-before in separate ALCs sharing CoreLib/hash witness/
process GC. Warm sync/async allocation is within0.015%; compile−0.335to+0.029%.
Admitted root peaks rise48B for search/text and96B for the other three scripts,
funding extra slot/metadata capacity. Identical tiny-budget thresholds are not a
contract. No RSS/live-heap/tail claim. Desktop text clocks show misleading30–50%
gains while other controls move up to36%; preserve them as diagnostic, not ordinary
CPU improvement. The reused driver's R45 purpose label is historical; R49's
identity/catalog/source patch define these jobs.

## Controlled quiet-VM comparison — B404

Same worker7cf37ca5/host/adapter27211dbe/normal PythonRe0.4 in four processes:
two qualified baseline and two identical current roles. Quiet AzureF4as_v7,
Ubuntu24.04.4/four EPYC9V45vCPUs/16GiB/no swap, .NET11RC3 normal GC/tiering.
Five-second warmup/role/job, seven rotated150ms rounds. Three existing warm jobs
complete87.49s.120s owner+30s stop+30s margin, observed after180s. All core/aux/
source/Ready/golden/funding/quiet/Closed/exit/join/fixture gates pass. Raw archive
9813698d verified under the collection lease and independently after retrieval;
no active VM workers/fixtures. No unchanged CPython remeasurement or full lane.

| Warm script | Baseline ms | Current ms | All current/baseline pairings | Current / baseline role spread |
| --- | ---: | ---: | ---: | ---: |
| Protected search |0.230179–0.230806|0.219034–0.220320|−5.10to−4.28%|0.59%/0.27%|
| Text/helper |2.009074–2.019131|1.904750–1.966849|−5.66to−2.10%|3.26%/0.50%|
| Ordinary JSON/helper |4.194609–4.283139|3.954709–3.964283|−7.67to−5.49%|0.24%/2.07%|

Ranges span role medians, not confidence intervals. Text/current spread and JSON/
baseline spread limit magnitudes. JSON is a representative control whose import/
helper bindings also change, not a promised zero-effect workload. Coarse external
process CPU moves consistently but is not isolated-method CPU or a latency proof.
Retain the coherent layout/publication correction and bounded elapsed improvement;
do not transfer desktop percentages, claim async elapsed improvement, repay older
regression debt or infer a universal speedup. The broader objective remains open.

Warm includes mediated RAM-file I/O, complete stdout/file validation and prior-result
cleanup. Compilation/startup/fixture staging/reset/transport/final cached teardown
are excluded. Compilation and async allocation diagnostics are separate. Primary
CPython tables still identify fully qualified source8c9fd0d7; no cross-run ratio
is constructed from those medians. Full platform/Stress/installed-package delivery
for this later source is pending, to be batched at this retained checkpoint.
Publication remains postponed.

[Controlled raw observations](b404/observations.tgz), [four-role summary](b404/comparison-summary.json),
[receipt](b404/archive-receipt.json), [precise allocation/adverse desktop results](local-summary.json),
[focused checks](focused.json), [semantic goldens](semantic-goldens.json),
[source/image identity](identity.json), [census](census.json), [unemitted declarations](dead-census.json),
[all development/failure/source/image evidence](development.tar.xz).
