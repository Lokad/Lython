# B1699 Shared closed default evaluation

The prototype replaces the separate computed single-definition evaluator
with the closed pair loop. It reads literal and captured defaults in tape
order and preserves the existing default and callable admissions, publication
and rollback. The frontend and retained metadata have no changes.

The source pair is `f6cf9796c` and the archived core patch. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Two workers per image contributed seven rotated batches each. Ranges span
all four pairings of process medians. Warm includes applicable RAM-backed
mediated I/O, output verification and result disposal. Through result adds
compilation and compiled-owner disposal. Wider CPU also includes request
framing and fixture reset.

| Workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Ordinary generated definitions, warm | +0.33% to +2.73% | +0.63% to +3.96% |
| Generated definitions with live aliases, warm | +0.48% to +1.57% | +0.44% to +2.04% |
| Defaulted rules, warm | −2.72% to −1.17% | −2.59% to −0.46% |
| Eager annotated rules, warm | +0.76% to +1.77% | +0.31% to +0.99% |
| Nested postponed annotations, warm | +7.09% to +13.58% | +7.48% to +13.43% |
| Defaulted rules, through result | −0.36% to +6.03% | −1.51% to +5.76% |
| Quoted invoice, warm sync | −0.83% to +0.58% | −1.67% to +1.06% |
| Original shipment, warm async | −3.97% to +0.15% | −4.31% to +0.00% |

Keep this patch private. The warm pair workload improves. The two generated
definition workloads and the eager annotation workload use more CPU. The
postponed workload is adverse even across workers, despite the 5.31%
candidate elapsed spread. Through-result CPU for defaulted rules spans
zero. A different representation would need to remove a stage of
construction or transport as well as the duplicated loop.

Both frozen images verify seven complete CPython 3.13.2 goldens. The live
variant retains all 96 callables across its selected calls and verifies
alias identity. All synchronous allocation and admitted peaks match on
the definition workloads. Allowance returns to baseline on all seven
cases. Focused Release passes 595 public cases each on Windows .NET 11
and Ubuntu .NET 10 and 81 Ubuntu white-box checks. Both quiet gates,
all 224 timed batches, worker cleanup, fixture removal and archive
verification pass.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Procedures and patch](procedures.tar.xz) · [Terminal status](quiet-terminal-status.json).
