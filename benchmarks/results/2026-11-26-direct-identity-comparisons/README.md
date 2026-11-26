# B1756 Share protocol-free identity comparisons

The trial uses the same comparison entry for `is` and `is not` in
expressions and branches. The entry reads the original operands in order and
uses the existing identity predicate. It removes the generic binary-operation
dispatch for these comparisons and its duplicate implementations. It
preserves the ordered numeric and custom comparison routes.

The reference is `d3d69f490`, whose runtime matches `4a273f36d`. Both
complete frozen images match four independent CPython output and file
goldens. The separately instrumented image counts 2,048 identity operations
in each complete shipment in both hosting modes. The retained-zip case has
three. File-fed map makes no calls to the changed comparison entry, including
failed attempts. Local CLR allocation and peak admission match on all four
cases. Allowance returns to baseline after cleanup.

Windows .NET 11 and Ubuntu .NET 10 each pass 1,117 public and 242 white-box
focused Release checks. The first Ubuntu run failed only on five CLI tests
requiring a `python` executable. The replay builds the matching Release
probe and supplies that command with CPython 3.13.2. Both logs remain.

The two November 26, 2026 UTC blocks use the same immutable workers on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Both warm each worker
for at least seven seconds and collect seven rotated batches. Ranges cover
the four pairings of process medians. Warm elapsed includes mediated RAM-backed
I/O, verification and result disposal. Process CPU also includes reset and
framing. All 140 batches completed with joined workers, both quiet gates per
block and verified archives.

| Block | Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | --- | ---: | ---: |
| Initial | Shipment callbacks | Sync | −4.70 to +2.80% | −3.93 to +1.78% |
| Initial | Shipment callbacks | Async | −9.19 to −7.02% | −7.58 to −5.28% |
| Initial | File-fed map control | Async | +0.51 to +6.94% | +0.36 to +5.61% |
| Reordered | File-fed map control | Async | +0.45 to +4.91% | +0.98 to +3.89% |
| Reordered | Shipment callbacks | Async | −9.92 to −5.09% | −10.36 to −4.57% |

Retain the shared comparison path. It removes generic dispatch for identity
and repeatedly reduces CPU on complete async shipments. Sync timing remains
inconclusive. The file-fed control has an adverse result in both blocks.
The route check finds no entry into the changed comparison code on that
workload. Keep the movement as open regression debt until attribution or a
qualified follow-up resolves it.

[Initial timing](summary.json) · [Reordered timing](confirmation/summary.json) ·
[Identity](identity.json) · [Route counts](comparison-attempts.json) ·
[Allocation and admission](allocation-summary.json) · [Initial observations](observations.tar.xz) ·
[Reordered observations](confirmation/observations.tar.xz) ·
[Procedures and checks](procedures.tar.xz).
