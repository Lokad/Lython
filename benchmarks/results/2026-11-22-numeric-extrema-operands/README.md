# B1335 Use existing operands for positional numeric min and max

Private `b6dd01aee` consumes exact numeric positional arguments in the
existing operand slots. It bypasses the positional list, synchronous
cursor and generic call path. The winning operand retains its identity.
NaN comparisons and ties keep the first argument. Keywords, iterable
calls and rich comparison remain on the general path, including async
callbacks. Stack admission and cancellation remain active.

Compared `52eeb328e` and `b6dd01aee` on November 22, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Each revision
uses two processes, seven rotated batches per process and at least
twelve seconds of warmup per process. Ranges span the four pairings of
process medians. Warm time includes mediated RAM-backed I/O, output
verification and prior-result cleanup. Time to result adds compilation
and code-owner disposal. Process CPU includes the wider request. Startup
and final teardown are separate.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Typed ASCII meter report | Warm | −15.74 to −13.12% | −16.74 to −13.02% | 1.77 / 1.32% |
| Explicit-float meter report | Warm | −12.80 to −11.58% | −12.50 to −11.07% | 0.64 / 0.76% |
| Typed Unicode meter report | Warm | −12.93 to −9.08% | −12.88 to −8.20% | 1.51 / 2.87% |
| Plain integer invoice | Warm | +0.16 to +2.47% | −0.96 to +2.36% | 0.06 / 2.24% |
| Typed ASCII meter report | Time to result | −16.43 to −13.81% | −16.40 to −14.44% | 1.83 / 1.28% |

The standalone candidate lowers local CLR allocations by 43.68% on
typed ASCII, 43.66% on typed Unicode and 30.64% on explicit float.
It removes about 1.25 million bytes per 4,096-row invocation. Invoice
allocation and all four admitted peaks match, and each owner refunds.
The combined diagnostic retains the B1334 slot ingress, which remains
private. Its allocation results concern that source pair only.

Retain the numeric-call change. All three affected reports show lower
warm elapsed, CPU and allocation, with lower ASCII time to result.
The sync invoice elapsed increase is a measured tradeoff with one
slower candidate worker. Its CPU comparisons span zero. The much
larger adverse B1334 sync-invoice result did not recur in this pair.

Windows .NET 11 and Ubuntu .NET 10 each pass 590 white-box and 776
public focused Release checks. Both 32-file images match four CPython
goldens. All 140 timed batches verify. The VM confirms that workers
join and the persistent archive verifies under lease before the run
ends. The original combined build failed on a missing namespace import,
which the standalone image includes.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
