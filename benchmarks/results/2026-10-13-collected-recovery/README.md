# Recovery from storage already collected by the CLR

Runtime revision: `7b8b0becd511f0635b178b6e48694c9c89955e99`; production tree
`7bbf8219299cd52347f74464d20ea2d08ddb0c0e`. Before: `b90f599b`, production
`e4039283`; the immutable Release package from `b43664e0` has that same production
tree. These are correctness checks on Windows/.NET 10.0.12 Release, 2026-10-13,
with ordinary runtime settings. There is no timing or embedding qualification.

After a pinned denial, allocation-only suppression also blocked the sweep of
owners subsequently collected by the CLR. A fitting replacement therefore still
failed even when its previous owner was already dead. The new rule separates
reconciliation of collected owners from permission to force another collection.

The existing pool records one CLR collection count at the **start** of each
complete sweep. An exhausted admission without allocation progress can perform
one complete sweep after new CLR collection, while keeping forced-GC suppression.
Partial sweeps cannot claim full coverage, and collection during traversal still
permits a later complete pass. Unchanged pinned denials do not repeatedly scan
the inventory. No root registry, owner identity/refcount, age tier, logical refund
or protection option is added; live aliases and pending finalizable storage keep
the existing accounting contracts.

| Validation | Before | Candidate |
| --- | --- | --- |
| Collected owner after pinned denial | Fitting admission fails | Succeeds, zero additional collections |
| Public release/retention controls, identical test DLL | 2 passed / 2 failed | 4 passed |
| Partial-sweep coverage and existing governor/pool controls | — | 27 passed |
| Public roots, native streaming and string admission controls | — | 52 passed |
| Complete Release suites | — | 10,849 passed |
| Exact isolated Release package consumer | — | Passed at 7b8b0bec |

The [white controls](../../../tests/Lokad.Lython.Tests/MemoryGovernorCollectedRecoveryTests.cs)
include 5,000 retained registrations whose owners are dropped after denial. A
partial sweep leaves collected owners beyond its quantum; the following pressure
check visits the remaining owners without another collection. The
[public controls](../../../tests/Lokad.Lython.PublicApi.Tests/CollectedMemoryRecoveryTests.cs)
hold a 16,000-byte string behind two tuple layers at a 28,000-byte cap and cause
one controlled host-side collection through `time.monotonic_ns`. Released data
permits replacement; retained aliases reject all 128 requests without additional
collections. Run and RunAsync use the same scripts. The host's test collection
is deliberate instrumentation, not new runtime behavior or a proposed host duty.

Original failed controls, process receipts/TRX and DLL identities are frozen in
private `.git/agent-notes/collected-recovery-20261013/`. The previous package and
candidate use the identical public test assembly; before-package bytes are never
modified. The exact Release package consumer passes at `7b8b0bec`.
Its package SHA512 is
`C7B1B95CA29920353623EAFABB39D176820663E25465290C648B42C5AFA72250C712C12381EE9D5D89A8897DC97E04392000853F75BDC9AAA985D21923F91CEC`.

Removing references **before CLR collection** remains a separate recovery bug;
this rule does not authorize collection merely because a reference changes.
Complete helper/child/resource representation and reclamation without forced CLR
collections remain open. See the [ownership assessment](../2026-10-12-memory-roots/OWNERSHIP-RULE-2026-10-13.md)
and [shared-frame recovery evidence](../2026-10-13-shared-async-frames/README.md).
