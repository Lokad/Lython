# Complete invoice call/publication attribution

R371/B760, 2026-11-14 UTC, committed4beb9ecf/frozenbed6e57e. One five-second
199Hz cpu-clock/fp capture after eight guest seconds of warmup, unchanged complete
4,096-row invoice, worker, adapter0.4 and catalog. Quiet F4as_v7/Ubuntu24.04.4/
.NET11RC3. PerfMap and same-process native disassembly are diagnostic overrides;
this is neither ordinary timing nor a causal comparison against the earlier image.

1,069guest leaves/5.38guestCPU seconds,81unknown and zeroambiguous. Same-process
Tier1 native body matches exactly11,172bytes/349instruction groups;103guest leaves
land in that body. All other thread CPU/raw stacks survive. Strict nearest owners
match the declaring assembly rather than mentions in parameter signatures.

| Guest leaf | Samples |
| --- | ---: |
| Ordinary instruction prefix /input dispatch |99 /50|
| Native main body /call dispatch /integer operations |103 /46 /39|
| CSV finish-record /feed |43 /34|
| Exact integer addition /machine-integer read |27 /20|
| Tuple backing copy |17|
| StartInline /StartNativeCall |17 /16|

These are mixed useful execution, not removable shares. The hottest native groups
include instruction fetch/checks, lookup and publication. Materialization still
appears, but counts do not establish a missing whole-frame cost or explain R370's
invoice regression. No magnitude prediction follows from this single capture.

Source inspection finds final private argument/local/operand storage and direct
binding already shared; no CallArgumentValue packaging on the small native path.
Deferred contexts and exact single-transfer continuations exist. Normal DictReader
adopts its funded field array; the parser stages at most one completed row, not a
queue. Split/case operations construct final UTF8 fragments/adopt backing. Tuple
copying shown here creates required independent immutable backing, not a second
discarded intermediate. Do not propose these completed removals again.

Close this bounded gate without a new generic call rewrite. Another arena/controller,
universal scalar representation, stack-check deletion or isolated scalar tweak is
not justified by these mixed samples. This is not a permanent ban. Ordinary input/
instruction publication and per-record construction remain source areas; a new
redesign needs an actual displaced stage with explicit semantic/lifetime acceptance.
Next, refresh changed complete CSV external comparisons at this retained delivery
checkpoint. R370invoice CPU debt remains explicit; the whole-script objective continues.

The original profile identity inherited stale sourceProofCommit/sourceProofSHA256/
fullDelivery template labels. Its sourceRevision, fullsource/catalog/core hashes
already identify actual4beb/bed; [explicit correction](identity-metadata-correction.json)
preserves original bytes and links the582-input commit proof/current CI. No retiming
or discarded samples. Original broader nearest-frame census remains beside the
strict declaring-assembly census, with their interpretation boundaries explicit.

Every image/auxiliary/golden/file/budget/quiet/join/archive gate passes. Two exact
duplicate rawJSONfiles deduplicate only after committed/local/remote archive member
and path/UID/link/fullhash proof under an idle lease. Compressed raw archives and
local Git bytes remain; no image/catalog/toolchain change. No new core/catalog
upload,254/252MiBfloors unchanged. Final free266,371,072B. Every RAM byte is archived
and independently verified before unmount; no users/fixtures remain. RawSHA256:
`51d2f0c6c5904a2eacbc6a90b524c9fcdd7ab355c7c8d7bf41a0d3a9b4f6e839`.

[Decision](decision.json),[source gate](source-assessment.json),[owning frames](leaf-census.json),
[native instruction groups](native-body-invoice-instruction-groups.json),
[raw](observations.tgz),[collection/analysis/custody](collection-and-analysis.tar.xz).
