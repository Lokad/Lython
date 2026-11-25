# Optional adapter single-group development replay

The private adapter now uses caller slots for one-shot matches with zero
or one capture group. With one group, its participation determines
`lastindex`. This also preserves the last successful capture of a repeated
group, a missing optional capture, and a named lookbehind capture outside
the selected match window. Matches with multiple groups still use the
backend's last-close metadata.

The comparison changes only the adapter DLL after the
[whole-match bridge](../value-match/README.md). The Lython core and
`0.5.0-dev.lython.6edc01772` producer binaries match exactly. Both images
match six complete CPython 3.13.2 goldens.

| One-group log search, local warm allocation | Previous | Candidate | Reduction |
| --- | ---: | ---: | ---: |
| Compiled pattern | 3,388,758 B | 1,938,656 B | 42.79% |
| Bound method | 3,487,140 B | 2,037,150 B | 41.58% |
| Module call | 3,585,356 B | 2,134,974 B | 40.45% |

The measurement uses Windows .NET 11. Allocation is identical for the
two-group and invoice controls. The no-group log search allocates 176 B
more per invocation. All admitted peaks match and budgets return to
baseline.

All 520 focused Release checks pass on Windows .NET 11 and Ubuntu
.NET 10. They cover in-flight cancellation and refused working storage
on the direct and cursor paths. The test and cancellation worker DLLs
match the development package for each target. The core has no producer
dependency. The production adapter keeps its existing pin.

## B1691 quiet-VM timing

The same six goldens verify against both immutable worker images. On
November 25, 2026 UTC, the Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11.
Two workers per image give seven rotated batches each per case. The
ranges span all pairings of worker medians. Negative values mean less
time. Warm includes mediated RAM-backed file reads, output verification,
result disposal and request handling. Compilation and startup are
separate. The core and backend DLLs match across both images.

| Warm workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Compiled pattern, one group | −18.26% to −16.22% | −19.16% to −14.52% |
| Bound method, one group | −17.58% to −8.75% | −18.22% to −9.90% |
| Module call, one group | −13.16% to −12.17% | −13.49% to −12.02% |
| Compiled pattern, no groups | +0.31% to +2.22% | +0.00% to +0.96% |
| Compiled pattern, two groups | +0.40% to +8.55% | +0.60% to +9.52% |
| Quoted invoice control | −1.21% to −0.35% | −1.06% to +0.60% |

The one-group cases use less elapsed and CPU across all pairings. The
bound-method candidate workers differ by 5.80% in elapsed. The no-group
elapsed is slightly adverse. The two-group path still uses the cursor
and its candidate workers differ by 7.90%, leaving the magnitude and
mechanism of the adverse result uncertain. Preserve both control
observations for another backend qualification. These timings apply
to the pinned development producer.

Both quiet gates and all 168 timed batches pass. The workers join,
temporary fixtures are removed, and the observations archive verifies
under the lease.

[Allocation](allocation-summary.json) · [Image identities](identity.json) ·
[Assembly verification](verification.json) · [Procedures and results](procedures.tar.xz).

[Timing](quiet-summary.json) · [Timed identities](timing-identity.json) ·
[Observations](quiet-observations.tar.xz) · [Timing procedures](timing-procedures.tar.xz) ·
[Terminal status](quiet-terminal-status.json).
