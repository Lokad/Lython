# Live function defaults and owned binding storage

Defs and lambdas now expose the tuple/dictionary actually used for positional and
keyword defaults. Assignment, deletion, `None`, type validation, supported container
subclasses and in-place keyword-default mutation affect binding. Defaults are read
after argument evaluation. Custom-key equality can genuinely suspend under
`RunAsync`; the current lookup retains its dictionary, and subsequent missing
parameters consult the function's current dictionary.

The old `FunctionBindingPlan.DefaultValues` field and its definition-time lookup
route are removed. Persistent per-default map fees are replaced by temporary
admission around construction: the live Python containers own retained storage.
Parameter-name maps/layout arrays also admit conservative capacity before
allocation. Failed construction refunds only new unpublished metadata, preserving
the supplied default values and aliases. No collector, reachability traversal,
native code or new configurable accounting mode is involved.

| Warm `RunAsync`, completed host | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| 16,384 calls using positional and keyword defaults | 9.166 ms | 9.555 ms | 1.258 ms |

The candidate median is **4.2% higher**. Baseline/candidate/CPython IQR/median is
3.0%/3.9%/3.3%. This single diagnostic establishes neither a qualified regression
nor a speedup, multiplier or confidence interval. Retain the independent semantics
and admission fixes; direct calls/layout sharing remain performance work. No samples
were replaced, and the producer was not retuned after measurement.

[Raw timing](warm-async.json) preserves seven rotating batches, warmup, calibration
and loaded identities. The owned group takes 7.988 seconds including cleanup under
a 30-second deadline. Timers include fresh invocation state, execution,
capture/projection, golden checks and prior-result cleanup. Compilation, startup,
transport and full response hashing are excluded. Lython includes async startup-
thread creation; CPython executes ordinary warm code, without injected I/O delay.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16,
ordinary GIL/GC, `-I -S -X utf8`. Public Lython policies and a shared allowance
remain enabled; GC/tiering are unchanged. CPython has no equivalent containment.

## Provenance and correctness

Baseline is 96596466349505f92fd0a689e2130f690a930dc6. The candidate was built before
commit on that revision plus thirteen production files identified by
[frozen hashes](comparison-declaration.json). One `calls.defaults` catalog case
was added identically to both roles; no framework changed. The same source and
golden cover all three sizes. [Baseline](baseline-correctness.json) and
[candidate](candidate-correctness.json) match CPython.
[Cleanup/source proof](cleanup-source-proof.json) verifies all seven comparison/
verification workers exited, exact source differences, identical harness/catalog,
inactive services and the machine lease.

Thirteen [independent Python programs](cpython-goldens.json) cover reads/identity,
replacement, deletion, invalid types, argument-time mutation, extra positional
entries, tuple/dict subclasses, lambdas and custom keys. Public tests exercise
both APIs. Additional cases prove genuine suspension, cancellation before refund,
retained result/default aliases, two executions sharing an allowance, refused
replacement without mutation, and unpublished-construction rollback.

Focused groups pass 71 white-box / 134 public cases, 103 governed CSV/text/JSON/file
controls and eight accounting cases including four original-scale stress cases.
The final portable/lifetime group passes 18 public cases. Groups overlap; do not
sum them. Every focused group includes cleanup and takes less than 30 seconds.
[Validation](validation.json) preserves receipts, TRX hashes and the initial failed
full run. Its 8,799 public cases pass; two white-box snapshots expected the old
reservation amount during generic definition. They now include default-map overlap
without changing callback order, step counts, frame depths or unwinding assertions.
The repaired full white-box group passes 2,152/2,152 in 12.386 seconds including
cleanup. Together with the unchanged producer's 8,799 public cases, all 10,951
ordinary cases pass. Package/platform checks are recorded at delivery; no unrun
full stress suite is claimed.

## Compatibility and accounting limits

[CPython 3.13.2](cpython-local-lookup.json) and
[3.13.16](cpython-vm-lookup.json) agree on the normal replacement witness: a later
parameter sees the new dictionary. Both can instead raise `TypeError` when an
allocating callback replaces its old dictionary's sole reference. Keeping an
explicit alias makes that file-backed witness succeed. Lython deliberately holds
the current lookup's storage across suspension and returns the deterministic
result in both variants. This is an explicit managed-lifetime compatibility edge,
not a claim to reproduce CPython's reference-lifetime behavior. Both observations
are retained; no broader reference-counting investigation is part of this delivery.

Layout storage is now admitted independently of the retired map fee. Ordinary
accounting fixtures use 64 definitions; the original 5,000-definition rejection
cases retain their 6 MiB limits in Stress. The original discarded-function batch
now explicitly refuses at 20 MiB and succeeds at a conservative 64 MiB. This is
earlier rejection with conservative per-execution ownership, not GC-independent
recovery after every overwritten function. Bounded host batches remain the
streaming contract; representative streaming controls pass. Sharing immutable
layouts is still open. Generated dataclass helpers use the live binding containers;
their general function-metadata surface remains unchanged.
