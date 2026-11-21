# B1281 shared backing for compound function defaults

The prototype uses a single compiler operand for initial default storage.
When a compiled function has both kinds of defaults, the positional tuple's
buffer holds the private keyword values after its logical end. Default
expressions still execute in source order. The definition pre-admits the final
keyword record and the temporary overlap. Access to `__kwdefaults__` copies
the prepared values into the final record and transfers its charge. A live
positional tuple retains its full original capacity.

The candidate is `c6d432634`. The reference `7e5cdd6e8` uses the exact B1280
image built from production-equivalent `0123f5e84`. Measured November 21, 2026
UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Two processes per source warmed for at least eight seconds and contributed
seven rotated batches each. Ranges cover all pairings of process medians.
Warm elapsed includes mediated RAM-backed I/O, goldens and prior-result
disposal. Time to result adds compilation and code-owner disposal.
Process CPU covers the wider request. Startup and final process cleanup
are outside the guest clocks.

| Complete script | Boundary | Elapsed change | CPU change | Local warm CLR allocation |
| --- | --- | ---: | ---: | ---: |
| Eager rules | Warm | −4.54% to +1.33% | −5.43% to +1.25% | −3.52% |
| Eager nested factories | Warm | −5.40% to −2.12% | −5.68% to −1.85% | −2.36% |
| Postponed nested factories | Warm | −5.46% to −2.34% | −5.81% to −1.79% | −2.57% |
| CSV invoice | Warm | +0.38% to +4.54% | +1.54% to +5.15% | +0.89% |
| Factory with default metadata reads | Warm | +2.85% to +4.80% | +2.56% to +5.65% | +3.20% |
| Fully supplied helper | Warm | −1.05% to +0.40% | −0.85% to −0.17% | −0.08% |
| Keyword dataclasses | Warm | −1.59% to −1.05% | −3.19% to −1.42% | +0.29% |
| Postponed nested factories | Compile to result | −5.66% to −2.51% | −5.72% to −1.03% | — |

The Windows .NET 11 and Ubuntu .NET 10 focused checks each passed 719
white-box and 2,728 public cases. The new public case observes tuple
length, negative indexes, copying, hashing and replaced defaults without
exposing the keyword tail. Both complete 32-file frozen images passed
seven CPython goldens. All local execution allowances refund to baseline.
Accounted peaks increase by 49,152 bytes on each of the factory cases
that use compound storage. The CSV invoice allocates 32,774 more CLR
bytes, consistent with the extra logical-length field on ordinary tuples.

The earlier local phase diagnostic at the reference revision identified
funded callable creation and allocation of defaults as larger targets than
ordinary binding. Instrumentation perturbed elapsed time on some scripts.
Those phase measurements are appropriate for attribution and separate from
the controlled source comparison.

Keep this layout private. Both nested factory workloads use less CPU,
and the postponed case reaches its result 2.51–5.66% sooner. Eager
rules alone have a 5.01% candidate control spread and mixed results.
The tuple-producing invoice and the metadata observer both consume
more CPU and allocate more. The invoice never uses compound defaults.
The results support a design that removes storage from ordinary
definitions without changing the tuple layout. A refinement also
needs to cover metadata observation without moving the entire
allocation cost to that read.

All 224 timed batches and both machine gates passed. Workers joined,
RAM fixtures were removed and the persistent archive was verified under
the lease.

[Timing](summary.json) · [Allocation](allocation-summary.json)
· [Phase attribution](factory-phase-attribution.json)
· [Raw observations](observations.tgz)
· [Source, checks and procedures](checks-and-procedures.tar.xz).
