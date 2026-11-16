# One shared replacement construction pass

R455/B859, 2026-11-16 UTC. Qualified be176ed0/frozen642f5115 reference;
private single-pass refinement at9ee6552d/frozene92d9598. The same32-file workers
differ only in core; normal optional adapter remains0.4.0. Production is retained
for shared semantics and the bounded no-op correction, **without a CPU gain claim**.
[Full platform/stress/installed-package delivery qualification passes](delivery/README.md).

One shared native-byte operation replaces the duplicate str/bytes construction
loops. Missing needles reuse the funded immutable receiver. Existing builders
still pre-admit capacity and replacement overlap, then perform their consuming
conversion. No all-match count/write pipeline, universal byte representation,
new cache/owner or memory-management mechanism remains. Empty needles retain
scalar boundaries for strings versus individual bytes for bytes. The existing
shrinking-byte final copy remains; this refinement does not claim its removal.

| Complete workload | Phase | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Text normalization, 4,096 lines /52,224 B | Warm | −0.57..+4.06% | −0.49..+4.80% |
| Same text script | Compile and run once | −3.23..+0.05% | −2.79..+0.19% |
| Binary normalization, same bytes | Warm | +1.00..1.52% | +0.20..2.47% |
| Same binary script | Compile and run once | +0.34..1.65% | −0.12..+2.86% |
| Ordinary positional helper/range,16,384 calls | Warm control | +0.96..2.89% | −0.79..+4.50% |

All pairings use two independent candidate and two reference medians, not
confidence intervals. Warm text reference/candidate controls move2.52/2.09%;
fresh text0.68/2.69%; warm binary0.23/0.28%; fresh binary0.95/0.35%;
calls0.07/1.85%. Maximum IQR1.71%. Allocation is essentially flat on all six
scripts; conservative peaks are unchanged. The two bounded no-op cases demonstrate
a storage benefit absent from these changing-data workloads.

The initial two-pass shape's9–15% binary elapsed cost motivates the different
algorithm, not another unchanged timing attempt. Current binary elapsed debt is
accepted explicitly for consolidation and correct bounded no-op behavior. Text
fresh trends favorable but controls make magnitude uncertain; calls and CPU
do not establish a gain. No further unchanged retiming is scheduled. This does
not explain older regression debt or close the whole-script performance objective.

Quiet AzureF4as_v7/Ubuntu24.04.4,4EPYC9V45vCPUs,16GiB/no swap,.NET11RC3
natural workstation GC/normal tiering. Five jobs148.196s,4s full-request warmups,
five rotated approximately0.5s samples/process. Warm includes mediated RAM I/O,
full output/file checks and prior-result cleanup; reset/protocol, compilation,
startup and final cache teardown are excluded. Fresh adds compilation and
compiled-owner cleanup. Process CPU includes reset/protocol. No CPython timing,
heap/RSS, pending-I/O or embedding-tail claim.

Actual Windows.NET11/10 each779public/99internal focused checks pass. Both exact
frozen workers agree with CPython on six scripts. Existing cases retain current
cancellation, actual protocol suspension, exact errors/order, first terminal
refusal, funded aliases and baseline reuse. Two new cases fail on the qualified
reference with memory exhaustion and pass here through both execution APIs while
an earlier result remains retained. Natural-GC diagnostics restore each compiled
budget baseline and final zero; no forced collection or local CPU measurement.

The full solution was built nonincrementally before promotion checks. The measured
binary corresponds to the private source identity, not a later rebuilt assembly.
All VM gates pass, users are joined, evidence was archived/SHA-verified under lease,
and fixtures are removed. Four retired raw working replicas have exact committed
canonical copies and fresh UID/path/hash/all-inode-alias proofs; source images,
toolchains and canonical evidence remain. An SDK-path preparation failure occurred
before any.NET10 test process started; the corrected actual target passes.

[Initial adversity and original failure](../README.md), [decision/all controls](decision.json),
[source/image inputs](identity.json), [sources](candidate-sources.tar.xz),
[observations](observations.tgz), [checks](focused-checks.tgz), [procedures](procedures.tgz).
No API or Python-semantics break is intended. Conservative thresholds are not exact
compatibility promises. Publication remains held.
