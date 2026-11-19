# B1133 completed file acquisition

R702 collapses the execution-state forwarding method into the opened file
owner. Synchronous and already-successful acquisitions attach the handle
directly. Pending and faulted asynchronous acquisitions use the owner's
await. The host still receives the original path, Cwd, token and budget. The file
owner attaches the handle before the post-acquisition cancellation check so
failure cleanup can retire it.

The quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 compared
reference `e7cb313d1` with private candidate `0f5733f3e` on November 19,
2026 UTC. Both images passed four stock CPython 3.13.16 goldens. Four
independent workers per case provided five batches apiece. Warm elapsed
includes mediated I/O, golden verification and prior-result cleanup.
Process CPU covers the wider batch. Each range crosses the two candidate
medians with the two reference medians. Negative changes mean less time.

| Workload | Warm elapsed change | Process CPU change |
| --- | ---: | ---: |
| Generated validator, 96 fields | −1.21% to +1.00% | −1.14% to +0.09% |
| Config manifest | −0.54% to +0.50% | −0.99% to +1.19% |
| CSV invoice | −1.93% to +1.91% | −1.45% to +1.45% |
| In-memory rule closures | −2.36% to −0.13% | −2.44% to +1.50% |

Local allocation was effectively flat across the four cases. Accounted
peaks were identical and the shared allowance returned to baseline. The
in-memory control's reference workers differed by 2.23% in elapsed time,
with CPU changes spanning zero. The two file workloads show no repeatable
timing gain. The candidate stays private. The removed forwarding await
alone does not justify the extra completion branches and does not account
for the acquisition samples in [B1132](../2026-11-19-validator-warm-profile/README.md).

Both Windows .NET 11 and Ubuntu .NET 10 passed 375 focused Release checks,
including pending opens, unpublished acquired handles, cancellation,
admission refusal, path intent and zip cleanup. The initial out-of-tree
checks missed two tracked workflow fixture directories. The rerun supplied
those fixtures and kept the same assertions. An initial catalog had an
incorrect version field type, and two VM preflights stopped for insufficient
working space. The archive preserves them. The measured 218-second run
passed both quiet gates, joined every worker, removed guest fixtures and
verified the persistent archive during the collection.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
