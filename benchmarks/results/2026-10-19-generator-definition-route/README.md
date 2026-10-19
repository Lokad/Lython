# Generator definition route removal

Immutable `eb3f674c` deletes the annotated/generic generator detour through lowered
function definition and its whole-namespace resynchronization. The existing
executable definition operation already constructs the same `PyGeneratorFunction`
and uses the same default, decorator, type-scope and metadata helpers. Generator
bodies, retained lexical namespaces and managed frame admission are unchanged.
No replacement engine, dispatch framework, public option or stack policy is added.
Lowered class bodies and yield-containing definition preparation remain supported.

70 focused white-box and 435 public Release checks pass. New public controls cover
annotation/default/decorator order, postponed annotations, docstrings, generic
shadowing, lazy bounds, escaped/nonlocal namespaces and inherited type scopes.
Real delayed host writes during annotation evaluation cover successful retained
results, cancellation, guest errors, terminal admission refusal, awaited cleanup
and budget reuse. The [eight pure cases](cpython.json) also match CPython 3.13.2.

One 7.596-second capture compares frozen `b2c91ba6` and `eb3f674c` with CPython
3.13.2. It derives `generators.annotated-drain.small` from the maintained
`generators.drain.small` by adding **only `->int`** to its function definition.
That annotation exercises the removed detour. The 256 yielded values, loop body,
fixture and independently derived golden are unchanged; this is a targeted
variant, not a new maintained benchmark family.

| Engine | Fresh compile and complete job | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `b2c91ba6` | 0.289 ms | 38.13% | Before; diagnostic |
| Lython `eb3f674c` | 0.308 ms | 25.73% | After; inconclusive change |
| CPython 3.13.2 | 0.043 ms | 13.86% | External context on this machine only |

There is **no demonstrated speedup or qualified regression**. The candidate median
is 6.58% higher amid much larger spread. Its Lython/CPython quotient, 7.13, is an
unqualified point estimate, not a current general runtime multiplier. Allocation,
warm-only execution and embedding latency were not measured. Do not retime this
small route removal as a new qualification campaign.

Each invocation compiles afresh and runs once in a running host, including output
checks and compilation/result cleanup; startup and worker protocol are excluded.
Compilation alone was not separated. Seven alternating rounds follow warmup on
shared workstation LOKAD-0399, x64 Windows build 26200, 28 logical processors,
.NET 10.0.13, workstation GC. All engines match exact output hashes before and
after timing. The owned parent and workers exited successfully.

Evidence: [raw samples and identities](micro.json), [process receipt](process.receipt.json),
[original maintained case](original-case.json), [annotated catalog](catalog.json),
[baseline identity](baseline.json), [scope and hashes](scope.json). The existing
comparison command and workers were used without framework changes. Dedicated
Linux snapshots in [BENCHMARKS.md](../../../BENCHMARKS.md) remain separate.
