# B1087 shared function construction

The private `a45b1b1b4` prototype shares the construction path for compiled
definitions, lowered definitions and lambdas. It removes the transient
reservation object for each function and keeps the same fixed capacity
estimate for the function, binding plan and inline metadata. Construction
checks cancellation after host admission, refunds failed construction and
preserves the existing funding for defaults, metadata and closures.

| Workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Unannotated rule closures, warm | −1.84 to +0.66% | −2.46 to +0.61% | 1.01 / 1.52% |
| Annotated rule closures, warm | −5.07 to −0.83% | −5.07 to −0.35% | 1.26 / 3.17% |
| Captured rule closures, warm | −2.22 to −0.91% | −3.16 to −1.11% | 0.74 / 0.60% |
| Reused defaults, warm | +3.49 to +9.90% | +3.90 to +9.92% | 3.33 / 2.77% |
| Positional calls, warm | −2.29 to −0.09% | −0.42 to +0.32% | 1.18 / 1.06% |
| Expanded forwarding report, compile-to-result | −0.82 to +0.96% | 0.00 to +1.39% | 1.68 / 0.11% |

Local warm allocation falls **4.61%**, **3.71%** and **4.26%** for the
ordinary, annotated and captured rule scripts respectively. Accounted peaks
match and each shared budget returns to baseline. Reused defaults creates its
function once per invocation and allocates 32 fewer bytes. Its sustained
elapsed and CPU increase remains unexplained. Keep the prototype private
while confirming this result with exact images and independently rotated
processes. The annotated and captured CPU gains justify that confirmation.

Each local target, Windows .NET 11 and Ubuntu .NET 10, passed 337 focused
Release cases. Both complete 32-file images match six CPython catalog
goldens. These cover the function shapes, mutable defaults, both APIs,
admission, cancellation and retained ownership exercised by the checks.

The source pair ran on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 on 2026-11-19 UTC. Each row spans the four combinations of
process medians. All 120 timed batches passed in 275.63 seconds. Both machine
gates passed, workers joined, fixtures were removed and the persistent
archive verified under the lease. An early retrieval attempt was rejected
locally by the observation guard before contacting the VM.

Warm elapsed includes mediated RAM-backed I/O, goldens and previous-result
cleanup. Compile-to-result also includes compilation and compiled-owner
disposal. Startup, fixture reset, request framing and final cache teardown
are outside guest elapsed. Process CPU covers the wider request.

[Elapsed, CPU and allocation](summary.json) · [Raw observations](observations.tgz) ·
[Exact source, checks and procedures](checks-and-procedures.tar.xz).
