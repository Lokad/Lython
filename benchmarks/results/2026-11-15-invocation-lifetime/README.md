# Scheduled invocation lifetime

R390/R391, 2026-11-15 UTC. **Retained for delivery; full qualification pending.**
Qualified reference **810379be / 8fbfa3f0** versus frozen candidate **f304b3db**.
The candidate's full source and binary are retained in the archive below. Full
platform, stress and installed-package qualification follows the bounded decision.

The change places setup, execution and ownership cleanup in the existing scheduled
operation. It removes the outer await/continuation for scope disposal. Caller
publication stays asynchronous, so an idle startup worker cannot run arbitrary
caller continuations and deadlock reentrant scheduling. Internal cancellation
relay now propagates cancellation state/token directly, preserving the public
outcome previously supplied by the outer async method. No new dispatcher, scheduler,
public options or memory infrastructure. Host setup callbacks move to the existing
worker under the captured context; there is no promised thread affinity.

Focused Release passes **193 public / 92 internal cases on each actual .NET 11/10**:
genuine suspension, worker reuse, context, cancellation, first admission refusal,
retained owners, cleanup and managed stack scenarios. Four new public cases cover
fault/cancellation during either execution-owner or result-owner creation, with no
guest side effects and cleanup of earlier owners.

| Quiet VM observation | Candidate elapsed change | Wider process CPU change | Controls / limit |
| --- | ---: | ---: | --- |
| Async captured text, initial | −0.24..+10.59% | −2.14..+5.74% | Reference spread 6.56%, candidate 4.03% |
| Async protected file search | −4.01..−0.40% | −12.90..−10.77% | Reference spread 0.37%, candidate 3.27% |
| Unchanged sync captured text | +0.05..+1.44% | −1.46..−0.25% | Reference spread 0.53%, candidate 0.85% |
| Async captured text, confirmation | +0.0003..+8.76% | −4.28..+6.75% | Reference spread 1.02%, candidate 7.11% |

Two independent processes per image, five rotating rounds. Each range spans all
four process-median pairings, not a confidence interval. Initial three jobs finish
in 161.868 s; the justified one-job confirmation in 66.649 s. The confirmation
preserves the first adverse observations and does not resolve text performance.
File-search CPU evidence is favorable; no general speedup is demonstrated.

The preceding local six-job diagnostic takes 88.473 s. File/text/CSV results are
mixed; short async helper elapsed is lower while its unchanged sync control moves
adversely. All raw observations remain; local results are not quiet-VM CPU claims.

Quiet Azure F4as_v7, Ubuntu 24.04.4, four EPYC 9V45 vCPUs, .NET 11 RC3, ordinary
workstation GC/tiering. Twelve-second full-request warmup for captured text, five
seconds for file search, 0.5 s request batches. Elapsed includes mediated RAM I/O,
public invocation, prior-result cleanup and full goldens; excludes compilation,
startup, reset/protocol and final cache teardown. Wider CPU includes reset/protocol.
No pending-I/O latency, heap/RSS, tail-latency or allocation claim.

The [completed shared-process method control](../2026-11-15-shared-hash-control/README.md)
retains 7.82% unchanged-reference spread despite identical CoreLib/hash witnesses
in its first process. Its second process favors the candidate by 3.74–5.35%
invocation time and 7.28–8.28% wider CPU. Hashing alone does not explain variability.
Retain the ownership-lifetime simplification, with all text adversity/uncertainty
and small sync-control debt explicit. No general speedup claimed; full delivery
qualification follows. This does not complete the performance objective.

Identity, goldens, quiet, shared-balance, Closed/join and archive gates pass in both
VM collections. No users or RAM fixtures remain; final free space 270,495,744 B.
Only 15 aliases of two superseded images were retired after byte-exact committed
archive custody and fresh metadata/hash checks. Current 7161/8fbf, raw archives,
catalogs, toolchains and 254/252 MiB floors remain. Read-only preparation caught an
absent older image path and a mismatched archived variant before any retirement;
the corrected custody is retained. No owned collection was restarted.

Raw collection identities retain inherited baseline `runtimeCommit`/source-proof
fields. The candidate's authoritative source/binary mapping is the separate
[589-input frozen identity](candidate-identity.json), not those baseline fields.
Publication remains postponed.

[Original prototype decision](decision.json), [retention and debt](retention-decision.json), [local observations](local-screen.json),
[initial quiet comparison](quiet/summary.json), [confirmation](confirmation/summary.json),
[source/binary, checks and procedures](sources-checks-and-procedures.tar.xz),
[initial raw evidence](quiet/observations.tgz), [confirmation raw evidence](confirmation/observations.tgz).
