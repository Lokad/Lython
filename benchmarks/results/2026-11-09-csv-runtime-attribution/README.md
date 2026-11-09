# Validated-CSV execution attribution

R162/B511–B512,2026-11-09. Maintained csv-validated-records.py,5,120records/68,625B,
R146 exact fixture/full golden. Frozen7c10322c-equivalent/corecfeb99b0/common worker
fec79 reused, not the subsequent argparse correction or scalar-comparison prototype.
The two argparse-only production deltas are explicit. Normal GC/tiering; only perf
maps enabled. This is CPU attribution, not a before/after or speedup measurement.

One corrected six-second capture after6s measured warmup yields6.119s execution
clocks,6.09guestCPU seconds/1,210guest samples,21caller/reset/protocol and19tiering.
Unknown guest leaves65(5.37%). Inclusive prefix contains useful/inlined work.
Offline phase-leaf detail extends the same raw capture; original assessor/output
are retained. No second timed capture was needed after the usable observation.

| Innermost observed guest category | Samples | Share |
| --- | ---: | ---: |
| VM prefix, useful work included |539|44.55%|
| CSV parser |194|16.03%|
| Scalar operators |86|7.11%|
| Admission |66|5.45%|
| Native-call preparation, including conversions/results |65|5.37%|
| Call binding |51|4.21%|
| Invocation setup, including useful prefix work |48|3.97%|
| Array adoption |21|1.74%|

Source gate rejects presumed extra row copying, bool arrays, eager frame namespaces
or second argument arrays: funded rows already transfer into dictionaries, presence
fits an inline word, workspace reused, arguments are final locals, frames deferred.
The header proof around admission protects reentrant mutation; do not delete it.
Integer parsing already consumes UTF-8. No new binder cache/arena/tagged VM selected.
These observations do not predict savings or close the whole-script objective.

Select R163's smaller coherent comparison consolidation: exact strings still
traverse generic operator/protocol/equality setup. Extend the existing exact scalar
entry and remove the executable string detour; preserve number/NaN, raw custom and
reflected protocols, actual async work, cancellation, terminal refusal and owners.
Acceptance and bounded source-pair/controls recorded before implementation. No
claim about an unmeasured prototype or final binary.

Original B5114096-execution warmup exceeded the unchanged20s frame guard **before
sampling**. Worker killed/joined; original source, partial record, maps, journal and
RAM inventory archived under a finite owned cleanup, locally SHA-verified before
temporary-map/RAM retirement. No usable capture, Closed or speed conclusion from
that failure. Original metadata's unused catalogSHA field still names config;
Ready/verify identify the actual CSV. It remains untouched in failed evidence.

Bounded B512 correction uses128warmup/64sampling executions per request, identical
script/fixture/freeze,6s targets,20s frame guard and120s owner+30stop+30margin. Exact
catalog SHA corrected and independently verified against Ready; source/fixture/full
output identities verified for every recorded batch. No relaxed guard, storage
deletion, runtime performance override or design-failure inference.

All corrected identity/quiet/golden/ownership/Closed/join/RAM/archive-under-lease/
localSHA gates pass. No remaining VM users/fixtures. Sampling boundary includes
wider batch work than execution clocks; no CPython/elapsed/embedding-tail claim.

[Phase/leaves](attribution.json), [source gate](source-gate.json), [source delta](current-source-delta.json),
[frozen sources](core-source.json), [identity](identity.json), [corrected raw](observations.tgz),
[original failed raw/maps](failed-attempt.tgz), [failure journal/record](failure-inspection.json),
[owned cleanup](failure-cleanup.json), [sizing correction](sizing-correction.json),
[preparation](preparation.tar.xz), [terminal](terminal.json).
