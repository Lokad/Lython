# Ordered native-key resolution and async lookup (B40)

This is a **correctness and deletion milestone**, without a speedup claim.
Baseline runtime is **1c369000**, unchanged at **3fe29ebf**. The final implementation
is that source plus [source.patch.gz](source.patch.gz), dated 2026-11-01.
[Summary](summary.json) records exact source/binary boundaries and controls.

## Demonstrated failures and shared implementation

The [original four probes](../2026-11-01-native-hit-protocol-order/README.md) show
native hits skipping an earlier same-hash custom key's equality error. The
verified original core fails **45 of 84** new independent semantic goldens.

Native-key operations now share one resolver using the existing funded protocol
snapshots and ordered storage positions. It checks earlier custom candidates,
revalidates their existing sequence identity after callbacks, and obtains live
values after mutation. No per-native-key ledger or new memory mechanism. Ordinary
tables retain direct storage; namespace bridges use their existing key order.
Candidate funding survives awaited equality and ends only after its user finishes.

Deleted: the separate builtin-key lookup/removal scans, the async string lookup's
candidate loop, repeated membership dispatch and native handling in the custom-key
write routine. Popitem removes its already-resolved stored entry without equality
callbacks. Hashing and write-key equality retain their synchronous restrictions.

Expanding the existing real-suspension witness then demonstrates another gap:
**all 11 lookup forms fail** on a native integer hit under RunAsync, because
string-only branches select a synchronous operation. [Failure log](native-types-before.log).
The resolver already accepts native keys. Removing those routing restrictions
and renaming the internal entry lets string/integer/tuple lookup equality await
the same implementation, including bound methods, descriptors, expanded calls,
comprehensions and executable/retained lowered paths. Key validation remains.
There is no legacy wrapper, extra engine, public option or dependency change.

## Acceptance and callback-count controls

The [public fixture](../../../tests/Lokad.Lython.PublicApi.Tests/Fixtures/DictionaryWrites/cases.json)
adds 84 CPython 3.13.2 goldens: both insertion orders, false/true/error callbacks,
reads, membership, writes, setdefault, pop/delete/popitem, reentrant mutations,
key identity and native derived mappings. Each runs through both APIs with shared
funding returning to baseline. Cancellation/refusal controls now include native
hits; refusal precedes equality effects and keeps its assignment origin. Retained
matched values stay charged across clear and a second execution.

Initial 72 probes have five exact stdout differences, confined to repeated
equality-call counts. All raw observations remain. Sixty fresh isolated CPython
process controls show those repeat counts varying in CPython too. The final
regressions assert callback presence, errors, one-time mutation, stored identities
and final values, rather than one hash-table layout's incidental repetition.
[Raw initial cases](native-hits-cases.json), [results](native-hits-results.jsonl)
and [independent repeat controls](cpython-reentrant-repeat-controls.json) remain.

Before the router correction, broad focused Release checks pass **246 native /
2,280 public per actual .NET 11 RC3 and 10.0.8**. Afterward, all **175 focused
semantic/lifetime cases per target** pass, including all 11 genuine-suspension
forms with native string/integer/tuple hits. Final build: zero warnings/errors,
fresh core/test/probe/benchmark output copies equal per target. Full delivery
local ordinary suites pass **3,956 native / 15,474 public per target**. Platform,
original-stress and installed-package outcomes are recorded in PLAN.md; local
logs do not imply their completion.

## Frozen native-only control — adverse result retained

One fixed complete text-script comparison, four warmups, seven batches of 128
jobs, ordinary tiering, dedicated Ubuntu 24.04.4 Azure F4as_v7 / four EPYC 9V45
vCPUs / 16 GiB, .NET 11 RC3. Warm synchronous public execution includes fixture
reset, real mediated I/O, full checks and declared cleanup; startup/compilation
are excluded. Shared unbounded funding exercises normal ownership paths; finite
refusal is qualified separately. No timing against incorrect mixed-key behavior.

| Metric per complete job | Before | First candidate | Identical before |
| --- | ---: | ---: | ---: |
| CPU ms | 6.08345 | 6.44787 | 6.36067 |
| Wall ms | 6.66869 | 6.82571 | 6.67194 |
| Allocated bytes | 5,518,769.875 | 5,518,681.9375 | 5,518,654.375 |

Candidate CPU **+5.99% / +1.37%** against the controls; control drift **+4.56%**.
Control validity passes the predeclared 5% CPU / 0.2% allocation bounds, but the
candidate exceeds the 5% regression diagnostic against the first baseline.
**That guard is flagged, not passed.** Allocation is effectively unchanged.
Magnitude remains uncertain; no latency improvement or qualified causal regression
is inferred. Correctness is retained, with this possible cost visible. No rescue
tweak or favorable retiming follows.

The timing measures [the first candidate patch](native-control-source.patch.gz),
before the independently demonstrated non-string async routing correction. It
must not be presented as final-source or CPython performance. Three processes,
22.223-second service runtime; quiet/golden/cleanup gates pass, all CPU batches
exceed 100 ms, workers join, service inactive, lease free. Exact loaded core
hashes match frozen inputs. Auxiliary benchmark/adapter sources are unchanged;
their build metadata differs and all loaded hashes remain visible in raw records.

[Raw results](raw-results.tar.gz), SHA256
`bb9dda558f38d57b08b6b7f4c97b8150e52112af4b89f93b2f410456aa9d63a8`, preserve
all samples, golden/ownership checks, source identities, catalog, machine gates
and collector logs. [Pre-code criteria](pre-code-plan.md) and readable existing
collector adaptations accompany them. Final/measured source patches are separate;
no observation is silently relabeled after the semantic routing change.
