# Call operand forwarding: withdrawn after bounded confirmation

R470/B876–B877, 2026-11-17 UTC. Qualified ccf20ba3 / frozen 7a599347 versus
private 71cd6ce1 + eight-file prototype / frozen 24f03c51. **Withdraw this shape.**
All eight production changes are removed; two independent precedence cases remain.
This is not a permanent design ban, a CPython refresh or a performance gain.

The compiler folds adjacent uncaptured/unmirrored local/constant argument leaves
into call metadata. The existing call boundary snapshots their ordinary funded
window before binding/admission; extent preserves the snapshot through promotion.
Cold spans support arbitrary argument indexes; histogram recognition consumes
prepared argument sources. The IR shows42fewer leaf instructions across4scripts,
with the existing histogram segment still prepared. No new backend, arena, pool,
frame controller, option or admission policy. Source acceptance did not predict CPU:

| First pair / complete workload | Phase | Elapsed change | Wider CPU change | Reference / candidate control spread |
| --- | --- | ---: | ---: | ---: |
| Helper-list normalization | Warm | −0.20..+3.02% | −0.20..+2.03% | 0.69 / 2.52% |
| Same script | Compile and execute once | −1.68..+2.65% | −1.60..+2.12% | 1.16 / 3.21% |
| Positional helper/range | Warm diagnostic | −1.10..+19.68% | −2.35..+19.52% | 0.38 / 20.55% |
| Same call diagnostic | Compile and execute once | +0.21..+3.44% | +0.35..+2.05% | 1.88 / 1.32% |
| Numeric squared-error report | Warm control | +2.74..+5.70% | +3.42..+5.58% | 1.00 / 1.86% |

One warm-call candidate decreases2.74→2.08ms across rounds; maximum IQR19.29%.
That is uncertainty, not a stable20% design regression. B876 uses sequential4s
full-request warmups, five rotated approximately0.5s samples/process; five jobs
finish152.52s. All original samples and failed stability conditions are retained.

One justified same-source confirmation interleaves warmup batches until each
process accumulates8s full-request time. Two jobs finish95.11s:

| Confirmation | Elapsed change | Wider CPU change | Reference / candidate control spread |
| --- | ---: | ---: | ---: |
| Positional helper/range warm | +0.48..+7.04% | +0.82..+9.47% | 0.003 / 6.52% |
| Numeric report warm | −6.20..+1.75% | −4.24..+2.46% | 5.33 / 2.99% |

Within-process IQRs improve (maximum0.96%), but between-process movement remains.
Confirmation establishes no call gain; numeric's original adversity is not erased
or causally explained. Ranges contain all four independent-process median pairs,
not confidence intervals. No source edit between collections, preferred-sign
retiming, automatic promotion or fixed-percentage veto. No further unchanged
confirmation/profiling. Added metadata/materialization work without demonstrated
benefit does not justify retention. Allocation/peaks are essentially unchanged;
local compilation allocation adds456Bcalls/1296Bhelper-text/632Bnumeric.

Initial actual Windows net11:105internal/242public, then25operand cases including
two new missing-argument/non-callable precedence cases. Actual net10:105/244.
Filters overlap and are not summed. After withdrawal the25operand cases pass
again on each actual target, with exact spans, no guest effects and restored
budget baselines. Existing tests cover genuine pending calls, aliases, cancellation,
terminal refusal, dictionary growth/loops and cleanup. The initial readonly-list
compiler error and corrected owned-array construction are in the logs. No test
assertion/timeouts relaxed; full qualification not replayed for a withdrawn runtime.

Nonincremental Release build; full32-file frozen images differ only in core.
Actual frozen workers/local CPython pass all12fixed-catalog goldens. Same catalog,
worker and optional0.4adapter in both collections; natural workstation GC/normal
tiering on quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3. Warm includes synchronous
Run, mediated RAM I/O, full stdout/files and prior-result cleanup; excludes compile,
startup, fixture reset/protocol and final cache teardown. Fresh adds compilation
and compiled-owner disposal; external CPU is wider. Independently warmed phases
are not additive. No async, RSS or embedding-tail gain claim.

All identities/goldens/quiet/Closed/exit/join/persistent-archive checks pass. Every
worker joined and RAM fixture removed. B876 retires only exact archived withdrawn
R469core/raw/metadata6,443,086B; B877 retires one exact archived old working trace
replica569,426B. Fresh ownership/path/SHA/link/all-inode-alias proofs precede deletion.
Current images/catalogs/toolchains/canonicals and254/252MiB staging/final floors
preserved. Terminals cached permanently; no requery/replay. Publication held.

[First summary](summary.json), [confirmation summary](confirmation-summary.json),
[original unstable condition](first-pair-condition.json), [allocation](allocation-summary.json),
[validation](validation.json), [first raw observations](first-pair/observations.tgz),
[confirmation raw observations](confirmation/observations.tgz), [final IR](forwarded-shapes.json),
[sources](candidate-sources.tar.xz), [prototype](prototype.patch.gz),
[identities](identity.json), [procedures/checks](procedures-and-checks.tar.xz),
[reference core](reference-core.dll.gz), [withdrawn core](candidate-core.dll.gz).
