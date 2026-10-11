# Immutable sequence storage accounting

Tuple and namedtuple construction use one conservative storage subtotal instead
of temporary scalar identity/refcount maps, staged refund lists and a separate
two-item adoption branch. The production change removes a net **84 lines**.
In two short replicas, the three-field tuple job takes **33.0% / 34.8% less time**,
and namedtuple construction takes **26.0% / 23.2% less time**. Each saves **368
managed bytes per record** in a separate construction-and-registration diagnostic.

This is a bounded part of the protection simplification work. Mutable scalar
maps, weak reclamation tiers, exhaustion GC and native stack telemetry remain.
The data does not establish a general runtime speedup or reduced process-wide
GC pauses. Sort medians rise within broad spreads and remain an assessment item.

## Accounting and compatibility

Immutable storage includes the existing backing estimate and 64 bytes per
numeric slot. Aliases keep Python identity but pay per occurrence. Small numeric
slots perform no identity lookup. Large integer magnitudes keep their independent
payload owners; only an unowned magnitude adds its payload-size estimate here.
There is no new owner type, configuration switch or execution-state cache.

Arrays fund one subtotal before copying. Enumerable construction retains its
temporary funding through materialization, commits only its own final storage,
and refunds scratch or failed construction. Owner snapshots release exactly the
committed subtotal when a dropped record becomes reclaimable. A counted source
that throws after yielding one scalar stranded 64 bytes on the baseline in a
private constructor regression; both counted and uncounted paths now refund
correctly. This does not demonstrate a previous public API failure.

Conservative slot charges can reject alias-heavy scripts under tighter budgets
and change accounted peaks. During assessment, charging an already-owned large
magnitude again per slot proved excessive: 1,000 three-field tuples sharing one
8,193-bit integer fit a 1 MiB cap on the baseline, but denied on the initial
candidate. The integrated candidate retains the existing owner lookup only for
large magnitudes. The same source succeeds in both execution modes at 429,309
accounted peak bytes versus 237,309 on the baseline. Unowned large payloads still
cannot fit a small-scalar allowance. Escaped large values retain their actual owner.

Bounded streaming remains live-memory accounting: 5,000 discarded tuple or
namedtuple rows complete under 1 MiB, although their cumulative record charges
alone exceed the cap. Retaining the rows denies under the same cap. This change
does not eliminate the CLR collection dependency of weak-target reclamation.
The contract is documented in [SPEC.md](../../../SPEC.md#146-memory-budgeting).

## Producers and validation

Baseline: `1dc58a76fb2da923bceaebf2d536a39969a8da2c`, production tree
`8d1cfbf35e3ad297885c3d73d548ade25d700c72`.
Candidate: `2856a132d50283420c14d944b6e78a3cbd4d1259`, production tree
`8a7da46048c76d77e6e641242a65738e245f8676`, test tree
`328d19584cc1b43b65b84a8a6ff43a11a65bb7ce`.
The comparison harness is unchanged at
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

All **9,925 Debug checks** pass: 1,892 white-box and 8,033 public. The VM passes
280 focused Release checks on the candidate and 267 on the baseline. Four pure
semantic probes agree with CPython in each execution mode. The five timing sources
verify against CPython on both producers before collection. Constructor denial,
funding transfer isolation, alias identity, cycles, escaped payloads, projection,
streaming, cancellation and the existing containment checks stay covered.

The initial candidate `811dc38b` and its excessive-charge receipts are retained
separately. The unpushed commit was refined before integration; its producer was
not overwritten. Initial test assertion failures and the baseline constructor
failure are also retained. Local Debug source trees match the refined candidate;
their builds preceded amended commit metadata. VM Release producers were built
from the respective committed heads. Binary and receipt hashes are recorded in
[evidence.json](evidence.json).

## Separate allocation diagnostic

Each shape retains 512 warmup and 10,000 measured records through process exit,
using the actual constructors, governor and registration pool with a 1 GiB limit.
Input boxes and compiled factory setup stay outside
`GC.GetAllocatedBytesForCurrentThread`. No forced collections occur. This measures
constructor-plus-registration managed allocation, not public invocation
allocation, native weak-handle storage, resident memory or CPython memory usage.

| Shape | Before bytes/record, replicas 1 / 2 | After bytes/record, replicas 1 / 2 |
|---|---:|---:|
| Two nonnumeric fields | 177.435 / 177.435 | 178.054 / 177.435 |
| Two distinct integers | 178.111 / 178.110 | 177.704 / 177.766 |
| Two aliased integer fields | 178.066 / 177.774 | 177.818 / 178.214 |
| Three-field tuple, two aliases | 553.407 / 553.407 | 185.407 / 185.407 |
| Three-field namedtuple, two aliases | 616.947 / 616.947 | 248.947 / 248.947 |

The three-field tuple saves 66.5% and namedtuple 59.6%. The earlier two-item
optimization already avoided maps, so those controls are essentially unchanged.
Logical fees are deliberately different for aliased numeric slots: retained
three-field populations rise from 3,663,104 to 4,335,872 accounted bytes, while
their actual measured managed allocations fall. Allocation savings are not a
promise of lower accounted peaks.

## Short execution diagnostics

Both producers use pinned SDK 10.0.401 / CLR 10.0.12 on the dedicated Ubuntu VM,
with isolated release CPython 3.13.16 as context. Workers retain ordinary limits,
tiering and GC. Only the controller disables tiering. Compilation, transport and
hashing are outside timing; ordinary execution and output checks remain inside.
No tests, builds or transfers run on the VM during collection.

Each micro gives every engine one second of warmup, approximately 25 ms batch
calibration and seven rotating-order rounds. The tuple source builds 2,048
`(i, i, i + 1)` records and checks length, final value and alias identity; namedtuple
uses the same fields. The jobs include comprehension and iteration costs.
The empty, integer-loop and full stable-sort sources are existing controls.

Cells show median **microseconds/invocation**, followed by **IQR/median**.

| Case / replica | Before | After | CPython |
|---|---:|---:|---:|
| Empty / 1 | 23.503 (3.9%) | 23.357 (1.9%) | 1.478 (0.4%) |
| Integer loop / 1 | 1104.857 (1.2%) | 1151.607 (1.3%) | 619.123 (0.2%) |
| Three-field tuples / 1 | 1103.379 (4.6%) | 739.449 (2.7%) | 54.634 (0.3%) |
| Namedtuple records / 1 | 2022.456 (3.4%) | 1495.796 (5.1%) | 301.941 (2.6%) |
| Full stable sort / 1 | 2641.483 (11.2%) | 2718.658 (3.6%) | 304.799 (0.8%) |
| Empty / 2 | 22.968 (2.5%) | 23.285 (5.2%) | 1.518 (0.5%) |
| Integer loop / 2 | 1106.046 (0.6%) | 1101.885 (1.9%) | 612.654 (0.7%) |
| Three-field tuples / 2 | 1124.837 (5.2%) | 733.589 (8.6%) | 54.517 (0.2%) |
| Namedtuple records / 2 | 2048.458 (5.8%) | 1572.224 (5.6%) | 303.773 (6.2%) |
| Full stable sort / 2 | 2721.641 (8.0%) | 2919.609 (17.6%) | 301.405 (0.8%) |

Target improvements repeat and exceed their spreads. The loop is 4.2% slower
in replica 1 and 0.4% faster in replica 2. Sort is 2.9% / 7.3% slower, with broad
spreads; this does not establish a reliable sort change. All observations are
retained without recollection. The result supports simpler immutable construction,
while whole-runtime and GC effects remain open.

Ten micros take 6.45–7.12 seconds each; cleanup completes in 7–8 seconds. Four
separate allocation groups, including cleanup, take 1–2 seconds. Every owned
group has a 30-second external cap, ends successfully, leaves an empty cgroup
and absent worker PIDs, and releases the VM lease. There are **no full lanes**,
milestone qualification or certified Lython/CPython multipliers.
