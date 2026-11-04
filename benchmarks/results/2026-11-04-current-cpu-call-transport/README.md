# Current whole-script CPU and call transport decision — B191

2026-11-04, committed334c7cc7 source, actual frozen precommit core
`80e9a32df6a11178b824d9dc229585dc817d0c85ec21d2f8c83bdae215db2e56`
(b4de9833 plus B190 final patch; identities/source hashes retained).
This is on-CPU diagnostic evidence, not a timing comparison or predicted speedup.
Existing checked protocol2, same quiet F4as_v7/.NET11RC3 Ubuntu VM, normal GC/tiering;
DOTNET_PerfMapEnabled=1 explicitly instruments CLR symbol maps. Source contract,
normal GC/profile and unsupported inlined/unknown work remain visible.

Exactly text/report complete warm scripts,6s execution warmup then2,048 verified
jobs each. perf cpu-clock199Hz, frame-pointer stacks and actual named active guest
TIDs; leaf IPs resolved against saved CLR maps. Native CLR symbols may contain raw
pointer notation; Lython source remains fully managed and memory-safe.
Persistent workers/catalogs/perf/maps/logs; guest files on tmpfs. All raw perf data
and maps archived and SHA256-verified before staging cleanup.

## Preserved failed conditions and completed captures

Initial shell launch failed before any workload due CRLF; preserve failed-crlf-launch.log.
After LF correction, text captured6.830s /6.78 guestCPU,1,347 samples, full before/
after goldens and normal Close/exit0. The following report recorder stopped before
sampling when profiler/runtime temporary files exhausted root disk; report metadata
write then failed too. Preserve partial-disk-failure.log and empty partial record.
No original immediate after-machine gate exists for this partial text campaign;
its data is diagnostic only, not silently upgraded to a full qualified campaign.

Confirmed all child processes terminal, removed checked inactive generated obj
caches only; source, frozen binaries and evidence preserved. One report-only capture
then succeeds: owned22.319s,10.765s observed batch /10.16 guestCPU,2,019 samples,
full goldens/ownership/normal close/exit0, before/after quiet gates pass.
One text-only confirmation supplies the missing complete gate boundary: owned17.767s,
6.48 guestCPU,1,289 samples, goldens/ownership/normal close/exit0 and both quiet gates
pass. Initial text remains separate. Combined measured guest batches below30s even
including the partial first text; no further capture campaign. No abandoned workers;
post-collection process inspection confirms terminal users. Final fixtures removed.

## Leaf evidence, not causal savings

| Sample group | Confirmed text | Report | Initial partial text |
| --- | ---: | ---: | ---: |
|Instruction prefix|19.24%|8.27%|17.52%|
|Ordinary calls|4.58%|0.30%|3.79%|
|Native return guards|1.86%|0.00%|2.75%|
|Native return evaluation|2.17%|0.00%|2.30%|
|Positional binding|0.85%|0.79%|2.23%|
|Monitor enter/exit|4.34%|10.35%|3.86%|
|ReadLine|2.72%|0.00%|3.04%|
|CopyFromUtf8|0.00%|1.88%|0.00%|
|JSON string reading|0.00%|4.46%|0.00%|

Unknown leaves5.8% confirmed text/12.0%report; CLR inlining/partial FP stacks and
small samples limit attribution. Do not add these disjoint leaf buckets to parent/
inclusive profiles or promise their removal as speedup. Confirmed text call/native/
binding buckets total~9.5%, partial~11.1%; prefix17.5–19.2% remains substantial.
Report has10.4% monitors and4.5% JSON ReadJsonString, including real work. Earlier
B175 data remains historical and overlapping; no CPU-only claim for B190.

## Concrete source decision

Native returns, shared driver, reused bound locals and module-frame elimination
ALREADY exist. Source confirms a remaining detour: BeginPendingCall rents/fills
BoundCallArguments BEFORE TryExecuteWithoutFrame. NativeReturn.cs recursively carries
that owner/presence record while only operand reads consume Values/formal count.
Replacing that representation can remove rent/copy/clear/return and metadata transport
for closed fixed-positional helpers, with the caller's admitted operands as the
synchronous borrow. All native guards remain before effects, generic suspending/
capturing/dynamic calls keep real owned binding, and output admission remains terminal.

Select B192 [closed-helper operand convention](selected-call-transport.md), using
existing NativeReturn nodes/binding metadata: no new IR, execution backend, arena,
pool, host policy or signature framework. The source/semantic/ownership gate is
written before code. This targets one real remaining call construction boundary;
it does not remove the whole general binder or constitute complete activation work.
No implementation or speedup is claimed by this diagnostic milestone.

Reader-to-string views are NOT selected now. Current CopyWindowRange copies each
complete line, but freezing/transferring buffer ownership, preventing compaction/
refund of live aliases, changing per-string metadata and keeping fresh-unpublished
refunds correct requires a larger explicit lifetime proof. Direct read/copy leaf
costs are not dominant enough here to automatically open that design. Preserve the
source hypothesis, not a permanent ban. Matched JSON snapshots ALREADY reuse metadata
and whitespace splitting ALREADY batches output admission; no re-proposal or timing.

observations.tgz SHA256:
`2213dfc85ad0fb7e72a378c0eaec7bf8aa3c10436fa2ebb61a22d719e92ea759`.
confirmed-text-observations.tgz SHA256:
`769f3241050bffbdd56b81ab769e26db3e4c5cc9fe2fe7685b2b8eeacac71cb7`.
Whole-script goal and fresh-JSON debt remain open; publication postponed. This is
not a new measurement framework, broad ownership audit or released-adapter qualification.
