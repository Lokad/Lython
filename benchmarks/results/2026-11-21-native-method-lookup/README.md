# B1295 ordinary instance lookup in native callers

Both executable drivers use the existing synchronous lookup for an
unshadowed instance method with the default attribute hook. A native
caller now records the selected method and receiver without creating a
caller context at that instruction. The existing continuation handles
custom lookup, other descriptors and genuine suspension. Method
selection still precedes argument evaluation and its side effects.
The implementation shares one direct lookup and one publication path
per driver. It adds no cache or workspace.

Reference `acb41ad00` and prototype `e8a74e81f` ran on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC.
Two independently warmed workers per revision contribute seven rotated
batches each. Ranges cover the four pairings of worker medians. Warm
elapsed includes execution, mediated RAM-backed I/O, golden checks and
prior-result disposal. Wider CPU includes the complete worker request.
Compilation, startup and final teardown have separate boundaries.

| Complete warm workload | Elapsed change | Wider CPU change | Local CLR allocation change |
| --- | ---: | ---: | ---: |
| Nested method relay, 2,048 input records | −35.03% to −32.36% | −35.39% to −33.57% | −71.72% |
| CSV invoice with nested policy methods, 4,096 data rows / 127,031 B | −12.52% to −10.94% | −12.84% to −11.15% | −29.92% |
| CSV invoice with free-function policy, same input | −1.00% to +1.81% | −2.94% to +2.54% | +0.002% |

Elapsed spreads are 1.89% / 2.17% for the relay, 1.43% / 0.37% for
the method invoice and 2.10% / 0.72% for the unchanged invoice.
Accounted peaks fall from 2,541 to 2,261 bytes in the relay and by
352 bytes in the method invoice. All five local workloads restore
their allowance to baseline. The local direct-method control
initially runs 4.79–11.09% slower with equal allocation. The separate
quiet control confirms a 6.07–9.18% slowdown. Keep this implementation
private and preserve the gains as a target for the native-only refinement.
[B1296 control](confirmation/README.md).

Windows .NET 11 and Ubuntu .NET 10 each pass 269 white-box and 1,558
public Release checks. A new test verifies the selected callable
survives real async host I/O and a method replacement during argument
evaluation. Both complete 32-file images match five CPython goldens,
and the three-lane quiet catalog verifies separately. A local staging
check initially counted allocation-probe files as part of the worker
manifest. The corrected check verifies all 32 worker hashes. Both
machine gates and all 84 timed batches pass. Workers join, guest
fixtures are removed and the archive verifies under the lease.

[Timing summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source, checks and procedures](checks-and-procedures.tar.xz).
