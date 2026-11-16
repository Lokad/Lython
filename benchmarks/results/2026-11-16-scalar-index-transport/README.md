# Shared scalar-index transport

R453/B856–B857,2026-11-16UTC. **Retain the coherent operand migration; [full delivery qualification passes](delivery/README.md).** Qualified reference81f60210/frozen a44f65a8 versus private
candidate at af5e6149 plus archived source/frozen642f5115. Only core differs in the
complete32-file images; normal optional adapter remains0.4.0. No public API change.

List,tuple,bytes and string reads share one existing-slot path in native helpers
and ordinary execution. A scalar index no longer needs publication as a guest
integer followed by conversion back. Remove both tuple-only borrowing routes;
the shared read keeps canonical tuple-item identities. General Python protocols,
slices,arbitrary-width keys and other receivers retain their existing behavior.
No new owner,arena,cache,code generator,public switch,quota,tracing or unsafe API.
Allocation preflights,current cancellation,terminal first refusal and output
funding remain; pending user protocols stay on the existing awaited path.

| Whole source / phase / collection | Elapsed change | Wider process CPU change | Reference / candidate process spread |
| --- | ---: | ---: | ---: |
| Numeric report / warm / B856 | −12.57..−5.99% | −10.70..−5.07% | 5.70 / 1.73% |
| Numeric report / compile-run / B856 | −15.88..+3.02% | −13.40..+5.74% | 10.14 / 11.19% |
| Normalization / warm / B856 | −3.11..+3.57% | −2.93..+4.44% | 5.98 / 0.87% |
| Ordinary calls / warm / B856 | −2.01..+1.71% | −2.08..+2.15% | 1.52 / 2.24% |
| Numeric report / compile-run / B857 confirmation | −10.26..−1.42% | −9.73..−2.35% | 6.83 / 2.83% |
| Ordinary calls / warm / B857 confirmation | −1.19..+0.69% | −1.98..+1.63% | 0.48 / 1.42% |

All four process-median pairings are retained;ranges are not confidence intervals.
Warm numeric and confirmed fresh elapsed/CPU favor the candidate,but moving
reference copies make magnitude uncertain. Original fresh includes adverse
pairings and10–11%moving unchanged controls;it remains inconclusive. Text/calls
are inconclusive. The confirmation does not establish that warmup caused the
variation or precisely explain the earlier R450 numeric/call regression debt.
No universal speedup,CPython ratio or cross-collection source gain is inferred.

Local natural-GC whole-process allocation for the complete numeric report falls
690,376→559,304B/invocation,**18.99%**. Conservative execution peak remains906,718B;
other four workloads change less than0.06%. All stdout/file goldens,compilation
budget baselines and disposal to zero pass. Counters include fixture reset,
mediated I/O,hashing/result cleanup;not compilation/startup/final cache teardown.
No forced GC,local elapsed/CPU inference,heap/RSS or embedding-tail claim.

B856 four jobs120.520s,4s full-request warmup/process,then five rotated~0.5s samples.
One bounded confirmation B857 two jobs97.237s uses8s warmup for both sources after
observing the moving unchanged fresh controls. Same sources,five-case catalog4253,
worker,flags,fixtures and goldens;only warmup/selected phases differ. Preserve the
original condition and all adverse results. Do not continue retiming for a desired
sign. Maximum IQR1.06/2.29/1.02/1.11%initial,0.73/1.08%confirmation.

Quiet AzureF4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3natural
workstation GC/normal tiering. Warm includes synchronous Run,mediated RAM I/O,
complete stdout/file checks and prior-result cleanup;excludes compilation/startup,
fixture reset/protocol and final cache teardown. Compile-run includes compilation,
one execution and compiled-owner disposal. Wider CPU includes reset/protocol.
No startup,pending-I/O,contention,physical-GC latency or embedding-tail measurement.

Both frozen workers agree with CPython on all five catalog cases before staging.
Final focused Release checks pass **176public/63internal each actual Windows
.NET11/10**,including negative/bool/huge indices,Unicode,helper aliases,retained
results,current-token cancellation,first refusal and budget reuse. Three new
cases retain helper-loop aliases and real asynchronous __index__ completion/
cancellation;existing __getitem__ cases remain. No architecture-only assertion,
timeout relaxation or exact-checkpoint counting. All2,369 frozen source inputs
(584production files) remain identical through measurement. Forced nonincremental
core build and matching test-bin core hashes recorded;rebuilt production binary
equality after promotion is not inferred. [Full platform/stress/package qualification passes](delivery/README.md).
Main-worktree nonincremental Release solution build passes both targets with zero
warnings/errors. An additional23public .NET11operand-error checks preserve the
original failing name,span and evaluation order;[promotion checks](promotion-checks.tar.xz).


All image/Ready/golden/quiet/Closed/exit/join/archive gates pass. Evidence SHAverified
persistently under the lease before RAM teardown;no users or fixtures remain.
Terminals cached forever. B856 retired only three historical working raw JSON
replicas,total6,927,685B,after committed archive/member equality plus fresh UID/path/
hash/nlink/all-alias proof. B857 retires nothing and reuses both source images by
verified hardlinks. All adverse canonicals,current images/toolchains and254/252MiB
floors remain. Original local archive-mapping/clock-replacement preparation errors
were rejected before VM launch;their corrections remain archived. No measurement
or source snapshot was overwritten.

This removes a real transport stage and establishes lower allocation. Broader
call/frame/text/frontend redesign remains open;publication/tagging held.

[Original four jobs](initial/summary.json),[bounded confirmation](confirmation/summary.json),
[allocation](allocation-summary.json),[exact image/source hashes](identity.json),
[source snapshot](candidate-sources.tar.xz),[core/checks/procedures](core-checks-and-procedures.tar.xz),
[decision](decision.json). Raw observations and receipts are alongside each summary.
