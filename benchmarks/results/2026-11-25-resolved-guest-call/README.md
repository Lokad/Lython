# B1688 Share resolved guest call targets

The private patch resolves an evaluated guest function and its bound receiver
once at the call instruction. Native dispatch, deferred and ordinary frame
entry, and general guest binding use that pair. The patch removes 15 net
source lines and allocates no new target object. Shape checks, descriptor
evaluation, operand publication, frame admission and cleanup stay in their
existing order.

On November 25, 2026 UTC, the Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11.
Both complete images match nine independent CPython 3.13.2 goldens. Their
only binary difference is the core DLL. The baseline is the B1687 runtime
at `0d08c3b1b`. Two workers per source supply seven rotated batches each.
Ranges cover all candidate/reference worker-median pairings. Positive
values mean longer. Warm includes mediated RAM-backed I/O when applicable,
output verification, result disposal and request handling. Through result
adds compilation and compiled-owner disposal in the warm worker.

| Workload and boundary | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Ordinary definitions, warm | −0.19% to +1.67% | −0.43% to +1.05% |
| Defaulted rules, warm | +0.87% to +1.69% | +0.28% to +1.97% |
| Original unbound shipment, warm async | −3.36% to +1.49% | −2.45% to +1.00% |
| Eager annotated rules, warm | −2.11% to +4.43% | −0.15% to +5.53% |
| Defaulted rules, through result | +2.74% to +7.35% | +5.55% to +6.80% |
| Bound shipment, warm async | −0.72% to +2.55% | +0.60% to +2.25% |
| Postponed annotated rules, warm | +0.77% to +2.24% | +0.89% to +2.91% |
| Quoted invoice, warm | +1.26% to +2.95% | +1.80% to +2.58% |

Keep the patch private. Defaulted time through result, postponed rules and
invoice use more CPU across every pairing. The original async shipment
spans zero and leaves the previous slowdown unresolved. The comparison
does not isolate the causes of either result. It weighs against extending
the selected target's lifetime across the current dispatch stages by itself.

Executable code and spans match across all 224 bodies and 2,278
instructions. Local warm allocation differences range from −168 B to +120 B.
Admitted peaks match and budgets return to baseline. Focused .NET 11
Release checks pass for 1,161 public cases on both Windows and Ubuntu and
171 Ubuntu white-box cases. All 224 timed batches and both quiet gates
pass. Workers join, temporary fixtures are removed and the archive verifies
under its lease.

[Identities](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Procedures and patch](procedures.tar.xz) ·
[Terminal status](quiet-terminal-status.json).
