# B1389 Reuse compiler block storage and assignment facts

Two private cores explore storage costs visible in B1386. Both use the
frozen `d45f2ed21` core as reference. Windows .NET 11 RC3 compiles each
source in seven batches of 64 per process after 160 warmup compiles,
in reference, candidate, candidate, reference order. Each invocation
disposes its code owner and checks that the allowance returns to
baseline. The local elapsed observations vary substantially across
processes. No quiet timing or comparative CPU claim was made.
[B1386](../2026-11-22-frontend-growth-attribution/README.md).

| Source | Reference allocated bytes | Reused instruction lists | Shared assignment snapshots |
| --- | ---: | ---: | ---: |
| Postponed nested factories | 65,400 | 65,496 | 65,832 |
| Inherited dataclass configuration | 530,536 | 530,584 | 530,536 |
| Sparse generated validator | 2,153,797–2,155,381 | 2,158,378–2,161,328 | 2,124,110–2,129,849 |
| Dead-heavy function | 840,568 | 840,616 | 834,992 |

The private `eb7536c34` change keeps cleared instruction lists in the
existing compilation workspace for later sibling bodies. Each builder
spends the same growth work in its first large body. The earlier packed
buffer also raised dense admitted peak. Leave this variant private and
look for a design that eliminates growth inside the body itself.
[B1333](../2026-11-22-packed-block-layout/README.md).

The private `bc5383f3b` change forks the standard immutable set builder
to share unchanged name facts across branches and comprehensions. It
eliminates eager copies, still scans shared facts when merging, and costs
more allocation in several smaller cases. The validator saves about
1.2–1.5% allocated bytes. Keep it private. A further design would need
to eliminate whole-set merge work as well as copying while preserving
deletion, isolated branch reads and diagnostic positions.

Each candidate produced identical executable instructions, stack layout
and source spans on 12 exact fixtures. The block-list candidate passed
513 focused Release checks on Windows .NET 11 and Ubuntu .NET 10. Initial
public runs missed scenario fixtures because the custom artifact
directory sat outside the worktree. The rerun supplied the same fixtures at the location the test loader
searches. The branch candidate passed 279 focused Release checks
on Windows, including unbound reads, comprehension scopes and deletion.

[Allocation and image identity](summary.json) ·
[Source, logs and raw observations](observations.tar.xz).
