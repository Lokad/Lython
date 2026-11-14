# Shared nonthrowing machine-integer arithmetic

2026-11-14UTC, R357. Retained frozen`cbb254be` vs fully qualifiedf5817a15/
frozen`9cfb7d5b`, four changed production inputs. Focused qualification and
source-pair comparison complete; full delivery qualification is pending.

Share the existing checked machine kernel with individual exact add/subtract/
multiply/floor-divide/modulo operations. Int128intermediates/explicit fit checks
remove expected-overflow exceptions from ordinary probes, chains and captured
cells. Exact int64results bypass general BigInteger limb arithmetic and repeated
major-estimation routing. Published values remain BigInteger objects, retaining
identities/aliases and exact wider promotion. Major allocation/workspace admission,
terminal refusal, cancellation and cleanup remain. No new value format/owner/
array/cache/arena/policy. This is a coherent kernel step, not completion of the
broader scalar or representative whole-script objective.

Quiet AzureF4as_v7, Ubuntu24.04.4, .NET11RC3; unchanged command-v2workers/data/
auxiliaries. Four fresh processes/job,5sfull-request warmup (12sfor captured text),
five0.5srotating batches, normal GC/tiering. Warm clocks include mediated RAMI/O,
stdout/file validation and result cleanup; exclude compilation/startup/reset/
protocol/final cache disposal. Wider CPU includes reset/protocol. No CPython,
startup, tail-latency or cross-collection speedup is claimed.

| Complete workload | Candidate warm ms | Elapsed change vs reference | Wider CPU change |
| --- | ---: | ---: | ---: |
|100,000-add numeric helper |4.114–4.136|−20.72..−19.67%|−21.41..−19.67%|
|4,096-row invoice |7.459–7.516|−0.62..+1.48%|0..+2.45%|
|4,096-row fulfillment |8.388–8.545|−0.38..+2.49%|0..+2.24%|
|3,072-line captured text |1.269–1.360|−12.52..+5.01%|−13.83..+5.13%|

Numeric-loop allocation is flat (~7.52MB/run); fulfillment is effectively flat
(−3.875..0B/run). This avoids arithmetic work, not boxed result publication.
Invoice/fulfillment show small adverse CPU direction: retain it as unresolved
regression debt, not neutrality. Text reference10.73%/candidate6.68%process
spreads prevent attribution. Retain the useful shared kernel without claiming
representative improvement or automatically retiming unchanged controls.

2,152focused cases pass:114internal/962public per actual .NET11/10. Signed
machine-boundary cross-products preserve promotion, Python signs/zero errors,
bool/reflected/in-place protocols, aliases, cancellation, terminal refusal and
retained-result/budget cleanup. Six isolated CPython3.13.2controls match the exact
frozen core; all8worker/source/case goldens and balance checks pass. B732completes
all4jobs in168.213s with identity/output/budget/quiet/join/archive gates. All
workers and owned RAM fixtures are gone; terminal results cached.

[Initial69d9prototype](initial/README.md) preserves its13–15%numeric benefit,
uncertain controls, typed-ingress fixture failures and four-case allocation
timeout/correction. Final staging initially selected the old candidate image where
the baseline reference was required; the identity guard failed before mutation/
launch. Corrected selection retains the samecbb2core/data/settings. Full original
scripts/config/logs survive. Only canonical-Git-proven obsolete prototype hardlinks
and duplicate completed raw archives were retired on the VM; current images,
catalogs, auxiliaries, toolchains and254/252MiBfloors remain.

[Decision/remaining work](decision.json),[timing](timing-summary.json),
[allocation](allocation-summary.json),[focused checks](focused-checks.json),
[independent Python](python-controls.json),[all sources/binary/checks/tools/raw](sources-checks-and-observations.tar.xz).
Publication remains postponed.
