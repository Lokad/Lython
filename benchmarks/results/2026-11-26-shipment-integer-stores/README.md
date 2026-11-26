# B1754 Transfer temporary integers into dictionary stores

This trial uses the existing native dictionary integer storage for a fresh
arithmetic result assigned to an existing exact string key. Published
aliases, growth, dynamic keys and arbitrary receivers use the original
assignment. Native storage and the shared memory allowance are unchanged.
The reference is `ba97a3ff8`.

| Workload | Unpublished RHS stores, reference | Direct stores, candidate | Lazy integer reads, reference → candidate | CLR bytes, reference → candidate |
| --- | ---: | ---: | ---: | ---: |
| Bound shipment | 2,048 | 2,045 | 0 → 2,045 | 1,167,876 → 1,167,664 |
| Ordinary shipment | 2,048 | 2,045 | 0 → 2,045 | 1,160,864 → 1,160,864 |
| Retained zip control | 0 | 0 | 0 → 0 | 1,092,536 → 1,092,536 |
| File-fed map control | 0 | 0 | 0 → 0 | 808,528 → 808,528 |

Route counts are the same in sync and async execution on the separately
instrumented binaries. Local allocation is a Windows .NET 11 sync
diagnostic on uninstrumented frozen images, with 2,048 records per workload.
Both images match the four independent CPython output and file goldens.
Peak admission matches in every case and cleanup returns the allowance to
baseline.

For the observed scripts this moves integer boxing from the store to the
next dictionary read. A later read publishes the canonical box once so that
identity and aliases remain observable. The 212-byte movement in the bound
case is under 0.02%. There is no allocation reduction attributable to the
proposed handoff. Skip quiet timing and leave the production and test patches
with the evidence. A future design must cover both sides or remove another
complete stage while preserving the value captured by `dict.get` before
any dynamic right-hand operation.

The trial passed 997 public Release checks on Windows .NET 11. Ubuntu
.NET 10 passed 985 public and 678 white-box focused checks. The public
filters differ. The two trial regressions agree with local CPython 3.13
on shared value identity, overflow, and target evaluation order.

[Identity](identity.json) · [Allocation and admission](allocation-summary.json) ·
[Initial route check](baseline-routes.json) · [Reference boxing](reference-boxing.json) ·
[Candidate boxing](candidate-boxing.json) · [Patches and procedures](procedures.tar.xz).
