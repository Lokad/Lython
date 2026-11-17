# Current text/floating CPU attribution

2026-11-17 B925, committedea8e9f91 production/frozencf847936, fixed32file comparison
image/optional adapter0.4, quiet F4as_v7/Ubuntu24.04.4/.NET11RC3. Normal GC/tiering
plus perf maps only. Two~5swhole-worker captures after8swarmup: text normalization
and floating squared-error report. Original actual output/file goldens, ownership,
quiet, joined users and archive-before-RAM-unmount/localSHA checks pass. No elapsed
comparison, startup measurement or new CPython ratio is inferred.

Text execution clocks4.699s/guest CPU4.49s; floating4.881s/guest4.80s. Whole-worker
capture includes reset/protocol/checks/I/O/cleanup beyond execution clocks. Keep
all-thread CPU/raw stacks, including text's~0.51sdistributed worker CPU and0.17sGC.
Selected active-thread text889samples/115unknown(12.94%), floating953/54(5.67%);
no malformed maps. Raw includes less-active threads; selected tables are not all
CPU or precise subsystem totals. Inclusive groups overlap useful/inlined work.

Text leaves: primary instruction prefix132samples, rstrip52, split44, with native
text methods/join/receiver calls and bulk scans also visible. Floating: primary
prefix136, secondary ExecuteStackTransfer86, sequence-index dispatch51, JSON
construction48, native-call preparation47, generic indexing42, native prefix35,
UTF8double parsing32 and yield buffering30. These are samples, not removable shares.
Ordinary function entry/binding/retirement has111inclusive floating samples and
none in selected text stacks; neither boxing nor frame allocation alone explains
the remaining gaps. Compilation's existing named checkpoint is near CPython and
does not justify another frontend rewrite from these warm samples.

Selected bounded source gate: collapse the owned runner's secondary transfer
switch into its existing primary loop, deleting ExecuteStackTransfer. Preserve
native helpers' distinct borrowed initialization after adverse R495 shared-runner
evidence; no new frame/operand/controller/policy. Preserve contexts, instruction
PCs/spans, suspension, alias snapshots/publication, cancellation, terminal refusal,
funded overlap and awaited cleanup. One actual11/10 focused/golden/reuse gate and
whole text/floating/call/control pair decides code-size/regression debt. No speedup
forecast; refinement remains possible for named evidence.

A source check also confirms generator expressions omit field-cache sites under
the older per-function lifetime rule. Keep this separate from the selected source
pair; enabling a cache is not a demonstrated explanation of the full gap.

Payload first preparation referenced a launcher absent from the old profile
folder; corrected locally before any VM staging. Only exact committed B924 raw/
archive replicas retire; currentcf/5dimages/catalog/toolchains stay. Original
254/252MiB guards retained. All32image hashes/catalog are verified before profiling.
[Attribution](attribution.json),[raw](observations.tgz),[procedures](procedures.tar.xz).
R499 full delivery is independent/pending; publication remains postponed.

Independent optional-adapter [source check](producer-source-check.json): producerb7adfc21 advances Core signatures/escaped-range correctness, but all four PythonRe metadata/cursor/type hashes remain unchanged from8403bc8e. Precise UTF8-name/token-lifetime/result-staging capabilities remain outstanding; core runtime work proceeds, no release or new qualification inferred. Committed blobs only; sibling untouched.
