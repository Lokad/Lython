# Conservative numeric set storage, 2026-10-11

Sets now retain one numeric-storage subtotal instead of a scalar identity/refcount
map. Each retained numeric member pays 64 bytes, including independently owned
magnitudes. Equal duplicates pay nothing more. Numeric growth is funded before
insertion; removal uses the actual stored member's type. This fixes leaked charges
when integer `0`/`1` is removed through equal Boolean values. First attachment
admits the existing shell, backing, numeric members and protocol storage before
publishing the governor, fixing incomplete and failed attachment accounting.

The production change removes **48 net lines** from `PySet`: its coupon field,
map allocations, pool-ownership lookups, staged adoption, scalar insertion
rollback and unreachable numeric protocol adoption. Other containers still use
the shared coupon class. Independent large payload owners, live reclamation,
forced GC, stack protection, host mediation, cancellation, allocation preflights,
cycle detection and bounded public projection remain in place.

The admission policy is deliberately stricter for owned numeric members and
previously unfunded nonempty attachment. Python equality, stored identities,
iteration invalidation and duplicate behavior remain unchanged. No cap was
raised and no original fixture shortened.

## Frozen sources and checks

Original diagnostic source is `8e8947b4`
on `diagnostics/set-numeric-storage-20261011`; the full identity is recorded in
[evidence.json](evidence.json). Its production tree is the preceding delivered
`576e3bf4c49bc159741e22f3e45d9b30a3c2db5a`. Nine original checks produce six
failures and three passes: both Boolean twin removals, incomplete attachment,
premature publication on denied attachment, and the proposed owned-member fee
fail. Duplicate admission and bounded clear/refill already pass. The owned-member
fee is a proposed policy change; the other failures expose accounting defects.

Candidate source is `b4864aa795044496813db46a8cc2e6af9d0de72f`, production
`17574145b945b85618f7647954bf24d7cadde9ed`. The unchanged original checks now
pass. Complete matching Debug suites pass **10,140 checks**: 1,973 white-box and
8,167 public. Normal VM Release passes **550 focused checks**: 209 white-box and
341 public. Original Release preserves six expected failures among 539 checks.

Four identical pure programs run in both modes at an unchanged **256 KiB** cap:
duplicates/Boolean twins, 100 × 1,000 clear/refill, repeated fresh-zero insertion
and Boolean removal, and 4,000 dropped large members. The original exhausts its
budget during repeated Boolean removal; the candidate completes and matches
CPython. All other cases match in both producers. Public checks also cover
retained large-member denial, dropped streaming, genuine delayed host I/O,
protocol hash/equality, denial, pool snapshots, construction failures and export.

The first validation wrapper supplied JSON objects instead of source strings;
the probe rejected it before executing a guest. The failed receipt is retained.
The corrected wrapper uses the identical source strings and budgets. Its
smoke verification also passes all four benchmark cases. No timing collection
was retried.

## Short diagnostics

Ordinary .NET 10.0.401 and CPython 3.13.16 workers ran on the quiet VM, with
normal tiering, GC and runtime guards. The supervisor's tiering override is
removed before worker startup. Each invocation creates a normal run; contexts
and scopes are not pooled. Eight fixed collections cover two passes of four
cases. Every collection has a 30-second hard limit including process cleanup.
These are diagnostic measurements, with **no ten-minute lane or milestone
qualification**. Source IDs, actual DLL hashes, worker identities, attempts,
outputs and cleanup receipts are in the evidence file.

| Case | Pass 1 change | Pass 2 change | Reading |
| --- | ---: | ---: | --- |
| 1,024 numeric set insertions | −6.17% | −5.46% | Focused improvement |
| Empty run | −2.07% | −0.90% | Small changes within broader spread |
| Basic integer loop | +2.75% | +4.09% | Retained slower control signal |
| Bounded CSV discard | −3.49% | −0.87% | Noisy control |

Numeric set medians move from 249.39 to 234.00 µs and 245.78 to 232.36 µs;
CPython medians are 38.68/42.73 µs. Baseline/candidate IQR fractions are
1.25%/1.49% and 1.44%/2.22%. The basic-loop control has low spread and slows
in both passes. Its cause is unestablished; this result supports no broad speed
claim. CSV spread is wider, especially the first candidate pass (7.96%).

The timing protocol has no allocated-byte metric. Map removal is source-proven;
native retained-byte savings and a general CPU benefit are not established.
All measured attempts complete. Owned workers/groups are stopped and empty,
the VM lease is free, source checkouts stay clean and producer DLLs rehash.
Both prior delivered list and integer-alias producers also rehash unchanged.

Remaining work includes dictionary/deque/property scalar maps, the accounting
dependency on forced collection, primitive storage policy and validated nesting
limits. This delivery does not complete the protection simplification plan.
