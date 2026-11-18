# Transfer completed abstract-analysis branches

B976 compared core `4eb89711` with `643f3980` from commit `58b7dd4d4` on
the Azure F4as_v7 quiet VM on 2026-11-18. Only the core differed between
the verified 32-file images. The .NET 11 RC3 workers used ordinary GC and
tiering. All four scripts matched CPython output and file goldens.

Selected branches transfer their completed name and sequence-bound maps.
Branches remain independently mutable and invalidate cached resolutions.
Joins use lookups for common keys directly, discarding the temporary
union/intersection sets and redundant left-side lookups. Deferred function
summaries keep their independent snapshots.

| Script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| CSV invoice, compile | −2.29 to −2.05% | −2.74 to −0.74% | 0.21 / 0.03% |
| Text helper list, compile-to-result | −2.07 to −0.89% | −2.00 to −0.78% | 0.50 / 0.70% |
| JSON config edit, compile control | −0.70 to −0.01% | −1.11 to +1.17% | 0.32 / 0.37% |
| CSV invoice, compile-to-result | −0.02 to +1.26% | −1.96 to 0.00% | 0.49 / 0.79% |
| Positional calls, warm control | −2.04 to +1.10% | −2.86 to +1.66% | 2.59 / 0.61% |

Ranges compare all pairings of two process medians per revision. Negative
favors the candidate. Each process had eight seconds of rotated warmup and
five samples. Compile includes disposal of compiled storage. Compile-to-result
adds mediated RAM I/O, output checks and result disposal. Startup stays
separate. Process CPU also includes reset and worker protocol. All jobs
completed in 233.07 seconds. Quiet endpoints, worker joins, fixture removal,
and the persistent archive hash were verified.

Local compilation allocation fell 0.73% for the invoice, 1.01% for the
text helper list and 1.43% for positional calls. Config edit was flat.
Execution allocation medians and accounted peaks matched, with balance
restoration. Both Windows targets passed 27 white-box and 447 public
Release checks. Thirty probes matched the exact ordered diagnostic codes
and messages on each hosting path. The first build identified one white-box
call to the removed method. The updated regression protects both cached facts
and storage independence. The first probe reader expected a different JSON
shape. The corrected comparison used the same source inputs.

Retain the simpler branch ownership and the measured compile/text benefits.
Complete invoice elapsed leans adverse while CPU is flat to favorable.
Carry that observation forward with the positional control spread. This pair
did not refresh the external timing baseline.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and validation](checks-and-procedures.tar.xz)
