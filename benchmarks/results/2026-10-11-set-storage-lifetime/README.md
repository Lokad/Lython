# Private set storage lifetime, 2026-10-11

Repeated failed set construction exhausted a 1 MiB execution budget despite
retaining only a bounded amount of guest data. The corrected implementation
releases private set storage when population fails, and releases internal
intersection/subset scratch storage on every exit. Successful construction keeps
the existing live-owner accounting. This is a correctness change with no
performance claim or ten-minute benchmark lane.

## Source and scope

Baseline delivery is `31bdfaf418e92bfdf56810a6617e160f4b4c3711`, production tree
`99071b61a21b34753099117d0ef0c5bcf1ef0e64`. The frozen VM producer is its identical
production predecessor `c362b2436d14d38999d55357a1a1a734508929dc`.
Candidate implementation is `e60a86b64030dae7beb0e5ffd89a4da7c0f4ddc7`, production
tree `02f1fa7cc0f4653f839375518cef997c6662f7ed`, tests tree
`04329dc03e97c173a31bb5403bbc69096d9029bd`. Benchmark and probe source trees are
unchanged. Qualified binaries, rejected test receipts and VM producer roots remain
preserved privately; [evidence.json](evidence.json) records identities and hashes.

The existing set refund helper now defines one rule for storage that has neither
escaped nor been registered. Constructor-wide catches replace the narrower
scalar-adoption catch. Literals, comprehensions, copied method/operator results
and materializers refund population failures before publication. Builtin
`set(iterable)` and its async implementation reuse the existing materializers,
removing their duplicate population loops. Private intersection/subset witnesses
use `finally` because they never escape, including after a successful operation.
No new owner class, registry, configuration switch or per-item bookkeeping is added.

Final owner registration stays outside population catches: registration denial
already refunds its snapshot. Early-registered executable displays, live mutable
receivers and partial copies exposed through a deep-copy memo keep their charges.
Host mediation, cancellation, preflights, cycle protection and bounded projection
remain in place. [SPEC.md](../../../SPEC.md) documents the private-storage rule.

## Demonstrated behavior

Each pure probe performs 5,000 operations at the same 1,048,576-byte cap. Both
ordinary and actual async execution modes were checked with isolated CPython
3.13.16 on the VM, using .NET SDK 10.0.401 Release builds.

| Workload | Frozen baseline, both modes | Candidate, both modes | CPython |
|---|---|---|---|
| Catch failed `list(generator)` | Completes | Completes | Completes |
| Catch failed `tuple(generator)` | Completes | Completes | Completes |
| Catch failed `set(generator)` | `MemoryError` | Completes | Completes |
| Repeated `set.intersection(list)` | `MemoryError` | Completes | Completes |
| Repeated `set.intersection_update(list)` | `MemoryError` | Completes | Completes |
| Repeated `set.issubset(list)` | `MemoryError` | Completes | Completes |

The unchanged production baseline also failed six constructor refund checks and
eleven public regression cases. An initial partial fix still failed copied
intersection; that receipt is retained. Its private witness set had no release
on either success or failure, which led to the final shared release rule.

## Validation and limits

- Full matching-Probe Debug suite: **9,960 passes** (1,909 white-box, 8,051 public).
- Focused Debug: **96 passes**; frozen VM Release: **186 passes**.
- Six pure probes match CPython in each mode on the candidate; the frozen baseline
  reproduces all four set failures. Local Debug also matches all six in each mode.
- Direct accounting checks cover lazy/sized source failure, capacity and copy
  denial, exact refunds, reserved scratch cleanup and an independently retained
  set. Public checks cover validation, collection denial, side effects, unchanged
  copied-method receivers, partial live update, genuine suspension and cancellation.
- Four owned VM diagnostic groups complete in **1–2 seconds including cleanup**,
  below their 30-second hard cap. Groups are empty, parent handles stopped,
  producer binaries rehashed and the shared lease available.

Accounted peaks remain close to the cap in several cases: other dropped owners
still require managed collection. The governor's forced-GC relief therefore
remains. This round neither measures a speedup nor establishes that full
collections have disappeared. Mutable scalar identity maps and the stack policy
remain separate pending simplification work.
