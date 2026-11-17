# Single subscript entry: withdrawn

R474/B885, 2026-11-17 UTC. Qualified dc480d38/frozen5ecb2599 versus private
two-file prototype d39419ba. **Withdraw both production edits.** Receiver/key
entry duplication and an unused call output were removed, retaining native
integer, existing dictionary cache and Python protocol consumers. No new owner,
metadata, policy or public API. Allocation and numeric accounted peak stayed flat.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference / candidate control spread |
| --- | --- | ---: | ---: | ---: |
| Floating JSON numeric report | Warm | −5.30..−1.89% | −5.28..−0.71% | 3.21 / 0.38% |
| Same | Compile and execute once | **+0.47..+2.19%** | **+2.81..+3.24%** | 1.62 / 0.09% |
| Integer numeric report | Warm | −3.70..+8.26% | −3.61..+10.11% | 6.28 / 5.77% |
| Helper text control | Warm | −0.38..+0.75% | −2.16..+0.82% | 1.11 / 0.03% |
| Floating report | Compilation | −1.38..+2.72% | −2.39..+0.45% | 0.89 / 3.24% |

The warm benefit remains evidence. The run-once priority, adverse wider CPU and
flat allocation do not justify this shape. Integer results remain uncertain;
this does not resolve R471's earlier integer debt. No causal diagnosis, permanent
design ban or further unchanged-source timing. Source simplicity alone does not
establish a CPU improvement.

Five jobs took192.24s on the quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3 VM,
natural workstation GC and normal tiering. Four independent processes/job,
interleaved6s full-request warmups and five rotated approximately0.5s samples.
Ranges pair all process medians; they are not confidence intervals. Warm includes
synchronous Run, mediated RAM I/O, complete stdout/file goldens and prior-result
cleanup; excludes compile/startup/reset/protocol/final cache teardown. Fresh adds
compilation/compiled-owner disposal; external CPU is wider. No async, CPython,
RSS or embedding-tail claim.

Both actual Windows.NET11/10 passed153public/20internal focused checks, including
real suspended indexing, cancellation, retained descriptors/aliases, refusal and
cleanup. Copied32file images passed actual-worker3case goldens for each source
and13candidate goldens. Natural-GC allocation diagnostics restore every shared
budget baseline; numeric accounted peak906,718B unchanged. Source snapshot,
nonincremental freeze build and checks are preserved. No tests specific to the
discarded architecture were added. Qualified production remains dc480d38; full
unchanged platform qualification is not repeated.

All image/golden/quiet/Closed/exit/join/archive checks passed, RAM fixtures removed
and terminal cached. Fresh path/UID/hash/link/all-alias proofs retired only
committed old replicas (6,165,233B), preserving current images/toolchains and
254/252MiB staging/final floors. No staging failure in this collection.
Publication remains held; continue the larger aggregation/frame source gate.

[Summary](summary.json), [allocation](allocation-summary.json),
[raw observations](observations.tgz), [custody](archive-receipt.json),
[exact sources](candidate-sources.tar.xz), [prototype](prototype.patch.gz),
[core](candidate-core.dll.gz), [identity](identity.json),
[procedures/checks](procedures-and-checks.tar.xz).
